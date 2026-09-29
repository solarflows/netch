#include "DnsCache.h"
#include "Based.h"
#include <cctype>

std::string DnsCache::ExtractKey(const char* packet, int len)
{
	if (len < 12)
		return "";

	int offset = 12;
	std::string domain;

	while (offset < len)
	{
		uint8_t labelLen = (uint8_t)packet[offset++];
		if (labelLen == 0)
			break;

		if ((labelLen & 0xC0) == 0xC0)
		{
			// Compression pointer in Question section
			if (offset >= len)
				return "";
			offset++;
			break;
		}

		if (offset + labelLen > len)
			return "";

		if (!domain.empty())
			domain.push_back('.');

		for (uint8_t i = 0; i < labelLen; ++i)
		{
			domain.push_back((char)std::tolower((unsigned char)packet[offset + i]));
		}
		offset += labelLen;
	}

	if (offset + 4 > len)
		return "";

	uint16_t qtype = ntohs(*(const uint16_t*)(packet + offset));
	return domain + "#" + std::to_string(qtype);
}

uint32_t DnsCache::ExtractMinTTL(const char* respPacket, int respLen)
{
	if (respLen < 12)
		return 5;

	uint16_t flags = ntohs(*(const uint16_t*)(respPacket + 2));
	uint8_t rcode = (uint8_t)(flags & 0x0F);

	if (rcode == 3) // NXDOMAIN
		return 5;

	if (rcode != 0) // Other error (e.g. SERVFAIL)
		return 0;

	uint16_t qdcount = ntohs(*(const uint16_t*)(respPacket + 4));
	uint16_t ancount = ntohs(*(const uint16_t*)(respPacket + 6));

	if (ancount == 0)
		return 5;

	int offset = 12;

	// Skip Question section
	for (uint16_t i = 0; i < qdcount; ++i)
	{
		while (offset < respLen)
		{
			uint8_t labelLen = (uint8_t)respPacket[offset++];
			if (labelLen == 0)
				break;
			if ((labelLen & 0xC0) == 0xC0)
			{
				if (offset < respLen)
					offset++;
				break;
			}
			if (offset + labelLen > respLen)
				return 5;
			offset += labelLen;
		}

		offset += 4; // QTYPE + QCLASS
		if (offset > respLen)
			return 5;
	}

	uint32_t minTTL = 300;
	bool hasTTL = false;

	// Parse Answer section
	for (uint16_t i = 0; i < ancount && offset < respLen; ++i)
	{
		// Skip NAME
		while (offset < respLen)
		{
			uint8_t labelLen = (uint8_t)respPacket[offset++];
			if (labelLen == 0)
				break;
			if ((labelLen & 0xC0) == 0xC0)
			{
				if (offset < respLen)
					offset++;
				break;
			}
			if (offset + labelLen > respLen)
				return hasTTL ? minTTL : 5;
			offset += labelLen;
		}

		// TYPE(2) + CLASS(2) + TTL(4) + RDLENGTH(2)
		if (offset + 10 > respLen)
			break;

		uint32_t ttl = ntohl(*(const uint32_t*)(respPacket + offset + 4));
		uint16_t rdlen = ntohs(*(const uint16_t*)(respPacket + offset + 8));
		offset += 10 + rdlen;

		if (!hasTTL || ttl < minTTL)
		{
			minTTL = ttl;
			hasTTL = true;
		}
	}

	if (!hasTTL)
		minTTL = 60;
	if (minTTL < 5)
		minTTL = 5;
	if (minTTL > 300)
		minTTL = 300;

	return minTTL;
}

bool DnsCache::Get(const char* queryPacket, int queryLen, std::vector<char>& outPacket)
{
	if (queryLen < 12)
		return false;

	std::string key = ExtractKey(queryPacket, queryLen);
	if (key.empty())
		return false;

	std::lock_guard<std::mutex> lock(cacheMutex);
	auto it = cacheMap.find(key);
	if (it == cacheMap.end())
		return false;

	if (std::chrono::steady_clock::now() >= it->second->expireAt)
	{
		lruList.erase(it->second);
		cacheMap.erase(it);
		return false;
	}

	// Move accessed item to head of LRU list
	lruList.splice(lruList.begin(), lruList, it->second);

	outPacket = it->second->responsePacket;
	if (outPacket.size() >= 2)
	{
		// Replace Transaction ID with query's Transaction ID
		outPacket[0] = queryPacket[0];
		outPacket[1] = queryPacket[1];
	}

	return true;
}

void DnsCache::Put(const char* queryPacket, int queryLen, const char* respPacket, int respLen)
{
	if (queryLen < 12 || respLen < 12)
		return;

	uint16_t flags = ntohs(*(const uint16_t*)(respPacket + 2));
	if (!(flags & 0x8000))
		return; // Not a response

	uint8_t rcode = (uint8_t)(flags & 0x0F);
	if (rcode != 0 && rcode != 3)
		return; // Do not cache SERVFAIL / REFUSED / errors

	std::string key = ExtractKey(queryPacket, queryLen);
	if (key.empty())
		return;

	uint32_t ttl = (rcode == 3) ? 5 : ExtractMinTTL(respPacket, respLen);
	if (ttl == 0)
		return;

	auto expire = std::chrono::steady_clock::now() + std::chrono::seconds(ttl);

	std::lock_guard<std::mutex> lock(cacheMutex);
	auto it = cacheMap.find(key);
	if (it != cacheMap.end())
	{
		it->second->responsePacket.assign(respPacket, respPacket + respLen);
		it->second->expireAt = expire;
		lruList.splice(lruList.begin(), lruList, it->second);
	}
	else
	{
		if (lruList.size() >= maxCapacity)
		{
			cacheMap.erase(lruList.back().key);
			lruList.pop_back();
		}

		lruList.push_front({ key, std::vector<char>(respPacket, respPacket + respLen), expire });
		cacheMap[key] = lruList.begin();
	}
}

void DnsCache::Clear()
{
	std::lock_guard<std::mutex> lock(cacheMutex);
	cacheMap.clear();
	lruList.clear();
}
