#pragma once
#ifndef PERSISTENTDNSCHANNEL_H
#define PERSISTENTDNSCHANNEL_H

#include "Based.h"
#include "SocksHelper.h"
#include <mutex>
#include <memory>
#include <atomic>
#include <vector>

class SingleDnsChannel
{
public:
	SingleDnsChannel() = default;
	~SingleDnsChannel() { Close(); }

	bool Query(const SOCKADDR_IN6* dnsServerAddr, const char* queryPacket, int queryLen, char* outBuffer, int& outLen, int timeoutSec = 2);
	void Close();

	std::mutex channelMutex;

private:
	std::unique_ptr<SocksHelper::UDP> udpClient;
	bool isConnected = false;

	bool EnsureConnectedLocked();
	void CloseLocked();
};

class PersistentDnsChannel
{
public:
	static PersistentDnsChannel& Instance()
	{
		static PersistentDnsChannel instance;
		return instance;
	}

	bool Query(const SOCKADDR_IN6* dnsServerAddr, const char* queryPacket, int queryLen, char* outBuffer, int& outLen, int timeoutSec = 2);
	void Close();

private:
	PersistentDnsChannel();
	~PersistentDnsChannel() { Close(); }

	PersistentDnsChannel(const PersistentDnsChannel&) = delete;
	PersistentDnsChannel& operator=(const PersistentDnsChannel&) = delete;

	static constexpr size_t POOL_SIZE = 4;
	std::vector<std::unique_ptr<SingleDnsChannel>> pool;
	std::atomic<size_t> roundRobinIndex{ 0 };
};

#endif
