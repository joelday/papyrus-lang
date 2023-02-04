#include "PexCache.h"
#include "Pex.h"
#include "Utilities.h"

#include <functional>
#include <algorithm>
#include <string>
#include <Champollion/Decompiler/PscCoder.hpp>
#include <Champollion/Decompiler/StreamWriter.hpp>

namespace DarkId::Papyrus::DebugServer
{
	bool PexCache::HasScript(const int scriptReference)
	{
		std::lock_guard<std::mutex> scriptLock(m_scriptsMutex);
		
		return m_scripts.find(scriptReference) != m_scripts.end();
	}

	bool PexCache::HasScript(const char* scriptName)
	{
		return HasScript(GetScriptReference(scriptName));
	}

	int PexCache::GetScriptReference(const char* scriptName) const
	{
		const std::hash<std::string> hasher;

		std::string name = NormalizeScriptName(scriptName);
		std::transform(name.begin(), name.end(), name.begin(), ::tolower);

		return std::abs(XSE::stl::unrestricted_cast<int>(hasher(name))) + 1;
	}

	std::shared_ptr<Pex::Binary> PexCache::GetScript(const char* scriptName)
	{
		std::lock_guard<std::mutex> scriptLock(m_scriptsMutex);
		uint32_t reference = GetScriptReference(scriptName);

		const auto entry = m_scripts.find(reference);
		if (entry == m_scripts.end())
		{
			auto binary = std::make_shared<Pex::Binary>();
			if (LoadPexData(scriptName, *binary))
			{
				m_scripts.emplace(reference, binary);
				return binary;
			}
		}

		return entry != m_scripts.end() ? entry->second : nullptr;
	}

	bool PexCache::GetDecompiledSource(const char* scriptName, std::string& decompiledSource)
	{
		const auto binary = this->GetScript(scriptName);
		if (!binary)
		{
			return false;
		}

		std::basic_stringstream<char> pscStream;
		Decompiler::PscCoder coder(new Decompiler::StreamWriter(pscStream));

		coder.code(*binary);

		decompiledSource = pscStream.str();

		return true;
	}

	bool PexCache::GetSourceData(const char* scriptName, Source& data)
	{
		const auto sourceReference = GetScriptReference(scriptName);

		auto binary = GetScript(scriptName);
		if (!binary)
		{
			return false;
		}
		std::string normname = NormalizeScriptName(scriptName);

		auto headerSrcName = binary->getHeader().getSourceFileName();
		if (headerSrcName.empty()) {
			headerSrcName = ScriptNameToPSCPath(normname);
		}
		data = Source(normname, headerSrcName, sourceReference);

		return true;
	}
}
