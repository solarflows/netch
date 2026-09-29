#pragma once
#ifndef DNSCACHE_H
#define DNSCACHE_H

#include <string>
#include <vector>
#include <list>
#include <unordered_map>
#include <chrono>
#include <mutex>
#include <cstdint>

struct DnsCacheEntry
{
	std::string key;
	std::vector<char> responsePacket;
	std::chrono::steady_clock::time_point expireAt;
};

class DnsCache
{
public:
	static DnsCache& Instance()
	{
		static DnsCache instance;
		return instance;
	}

	bool Get(const char* queryPacket, int queryLen, std::vector<char>& outPacket);
	void Put(const char* queryPacket, int queryLen, const char* respPacket, int respLen);
	void Clear();

private:
	DnsCache() = default;
	~DnsCache() = default;

	DnsCache(const DnsCache&) = delete;
	DnsCache& operator=(const DnsCache&) = delete;

	size_t maxCapacity = 2048;
	std::mutex cacheMutex;
	std::list<DnsCacheEntry> lruList;
	std::unordered_map<std::string, std::list<DnsCacheEntry>::iterator> cacheMap;

	static std::string ExtractKey(const char* packet, int len);
	static uint32_t ExtractMinTTL(const char* respPacket, int respLen);
};

#endif
