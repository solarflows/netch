#include "PersistentDnsChannel.h"

bool SingleDnsChannel::EnsureConnectedLocked()
{
	if (isConnected && udpClient && udpClient->tcpSocket != INVALID_SOCKET && udpClient->udpSocket != INVALID_SOCKET)
	{
		return true;
	}

	CloseLocked();

	udpClient = std::make_unique<SocksHelper::UDP>();
	if (!udpClient->Associate())
	{
		udpClient.reset();
		return false;
	}

	if (!udpClient->CreateUDP())
	{
		udpClient.reset();
		return false;
	}

	isConnected = true;
	return true;
}

void SingleDnsChannel::CloseLocked()
{
	if (udpClient)
	{
		udpClient.reset();
	}
	isConnected = false;
}

void SingleDnsChannel::Close()
{
	std::lock_guard<std::mutex> lock(channelMutex);
	CloseLocked();
}

bool SingleDnsChannel::Query(const SOCKADDR_IN6* dnsServerAddr, const char* queryPacket, int queryLen, char* outBuffer, int& outLen, int timeoutSec)
{
	for (int retry = 0; retry < 2; ++retry)
	{
		if (!EnsureConnectedLocked())
		{
			continue;
		}

		if (udpClient->Send((PSOCKADDR_IN6)dnsServerAddr, queryPacket, queryLen) == queryLen)
		{
			timeval timeout{};
			timeout.tv_sec = timeoutSec;

			int size = udpClient->Read(NULL, outBuffer, outLen, &timeout);
			if (size > 0 && size != SOCKET_ERROR)
			{
				outLen = size;
				return true;
			}
		}

		// On timeout or socket error, reset connection and retry once
		CloseLocked();
	}

	return false;
}

PersistentDnsChannel::PersistentDnsChannel()
{
	for (size_t i = 0; i < POOL_SIZE; ++i)
	{
		pool.push_back(std::make_unique<SingleDnsChannel>());
	}
}

bool PersistentDnsChannel::Query(const SOCKADDR_IN6* dnsServerAddr, const char* queryPacket, int queryLen, char* outBuffer, int& outLen, int timeoutSec)
{
	// 1. Try to acquire an idle channel without blocking
	for (size_t i = 0; i < POOL_SIZE; ++i)
	{
		if (pool[i]->channelMutex.try_lock())
		{
			std::unique_lock<std::mutex> lock(pool[i]->channelMutex, std::adopt_lock);
			return pool[i]->Query(dnsServerAddr, queryPacket, queryLen, outBuffer, outLen, timeoutSec);
		}
	}

	// 2. All channels currently busy, round-robin queue on one
	size_t idx = (roundRobinIndex.fetch_add(1, std::memory_order_relaxed)) % POOL_SIZE;
	std::lock_guard<std::mutex> lock(pool[idx]->channelMutex);
	return pool[idx]->Query(dnsServerAddr, queryPacket, queryLen, outBuffer, outLen, timeoutSec);
}

void PersistentDnsChannel::Close()
{
	for (auto& ch : pool)
	{
		if (ch)
			ch->Close();
	}
}
