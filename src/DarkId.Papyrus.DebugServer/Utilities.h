#pragma once

#include <string>
#include <sstream>
#include <regex>
#include <boost/algorithm/string/replace.hpp>

namespace DarkId::Papyrus::DebugServer
{
	template<typename... Args>
	std::string StringFormat(const char* fmt, Args... args)
	{
		const size_t size = snprintf(nullptr, 0, fmt, args...);
		std::string buf;
		buf.reserve(size + 1);
		buf.resize(size);
		snprintf(&buf[0], size + 1, fmt, args...);
		return buf;
	}

	template <typename T>
	T ByteSwap(T val)
	{
		T retVal;
		const auto pVal = reinterpret_cast<char*>(& val);
		const auto pRetVal = reinterpret_cast<char*>(& retVal);
		const int size = sizeof(T);
		for (auto i = 0; i < size; i++)
		{
			pRetVal[size - 1 - i] = pVal[i];
		}

		return retVal;
	}

	inline std::vector<std::string> Split(const std::string& s, const std::string& delimiter) {
		size_t posStart = 0, posEnd;
		const auto delimLen = delimiter.length();
		std::vector<std::string> res;

		while ((posEnd = s.find(delimiter, posStart)) != std::string::npos) {
			auto token = s.substr(posStart, posEnd - posStart);
			posStart = posEnd + delimLen;
			res.push_back(token);
		}

		res.push_back(s.substr(posStart));
		return res;
	}

	inline std::string Join(const std::vector<std::string>& elements, const char* const separator)
	{
		switch (elements.size())
		{
		case 0:
			return "";
		case 1:
			return elements[0];
		default:
			std::ostringstream os;
			std::copy(elements.begin(), elements.end() - 1, std::ostream_iterator<std::string>(os, separator));
			os << *elements.rbegin();
			return os.str();
		}
	}

	inline bool ParseInt(const std::string& str, int* value, std::size_t* pos = nullptr, const int base = 10)
	{
		try
		{
			*value = std::stoi(str, pos, base);
		}
		catch (void*)
		{
			return false;
		}

		return true;
	}

	inline void ToLower(std::string &p_str){
		for (int i = 0; i < p_str.size(); i++){
				p_str[i] = tolower(p_str[i]);
		}
	}

	inline std::string ToLowerCopy(const std::string &p_str){
		std::string r_str = p_str;
		ToLower(r_str);
		return r_str;
	}

	inline bool CaseInsensitiveEquals(std::string a, std::string b)
	{
		ToLower(a);
		ToLower(b);

		return a == b;
	}

	inline std::string DemangleName(std::string name)
	{
		if (name.front() == ':')
		{
			return name.substr(2, name.length() - 6);
		}

		return name;
	}
	inline std::string NormalizeScriptName(std::string name)
	{
		boost::algorithm::replace_all(name, "\\\\", "/");
		boost::algorithm::replace_all(name, "/", ":");
		boost::algorithm::replace_all(name, ".psc", "");
		//std::regex_replace(std::regex_replace(std::regex_replace(name, std::regex("\\\\"), "/"), std::regex("\\.psc"), ""), std::regex("/"), ":")
		return name;
	}
	inline std::string ScriptNameToPathPrefix(std::string name) {
		// TODO: We really need to stop relying on the file name in the compiled script header for the source name. This is just for testing.
		// Most Fallout4 scripts will not be found.
		// Get it from pyro?
		//F:\\Games\\fallout_4_mods_folder\\mods\\Auto Loot\\scripts\\Source\\User\\AutoLoot\\dubhAutoLootQuestScript
		//name = std::regex_replace(name, std::regex("[fF]:\\\\Games\\\\fallout_4_mods_folder\\\\mods\\\\Auto Loot\\\\Scripts\\\\Source\\\\User\\\\"), "");
		//name = std::regex_replace(name, std::regex("[Gg]:\\\\_F4(\\\\[\\w\\d_]+)?\\\\Art\\\\Raw\\\\Scripts\\\\"), "");
		boost::algorithm::replace_all(name, ":", "/");
		return name;
	}
	inline std::string ScriptNameToPSCPath(std::string name) {
		name = ScriptNameToPathPrefix(name);
		boost::algorithm::replace_all(name, ".psc", "");
		boost::algorithm::replace_all(name, ".pex", "");
		return name + ".psc";
	}
	inline std::string ScriptNameToPEXPath(std::string name) {
		name = ScriptNameToPathPrefix(name);
		boost::algorithm::replace_all(name, ".psc", "");
		boost::algorithm::replace_all(name, ".pex", "");
		return name + ".pex";
	}

}
