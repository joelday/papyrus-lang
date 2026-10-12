#include "PexCache.h"
#include "Pex.h"
#include "Utilities.h"

#include <functional>
#include <algorithm>
#include <string>
#include <Decompiler/PscCoder.hpp>
#include <Decompiler/StreamWriter.hpp>

namespace DarkId::Papyrus::DebugServer
{
	bool PexCache::HasScript(const int scriptReference)
	{
		std::lock_guard<std::mutex> scriptLock(m_scriptsMutex);

		return m_scripts.find(scriptReference) != m_scripts.end();
	}
	bool PexCache::HasScript(const std::string& scriptName)
	{
		return HasScript(GetScriptReference(scriptName));
	}
	
	std::shared_ptr<Pex::Binary> PexCache::GetCachedScript(const int ref) {
		const auto entry = m_scripts.find(ref);
		return entry != m_scripts.end() ? entry->second : nullptr;
	}

	std::shared_ptr<Pex::Binary> PexCache::GetScript(const std::string& scriptName)
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

	bool PexCache::GetDecompiledSource(const std::string& scriptName, std::string& decompiledSource)
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

	bool PexCache::GetSourceData(const std::string& scriptName, dap::Source& data)
	{
		const int sourceReference = GetScriptReference(scriptName);

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
		data.name = normname;
		data.path = headerSrcName;
		data.sourceReference = sourceReference;
		return true;
	}

	void PexCache::Clear() {
		std::lock_guard<std::mutex> scriptLock(m_scriptsMutex);
		m_scripts.clear();
	}
}
