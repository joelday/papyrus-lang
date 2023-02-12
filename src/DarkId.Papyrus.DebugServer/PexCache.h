#pragma once

#include <Champollion/Pex/Binary.hpp>
#include <map>

#include <dap/protocol.h>
#include <mutex>

namespace DarkId::Papyrus::DebugServer

{
	class PexCache
	{
	public:
		PexCache() = default;

		bool HasScript(int scriptReference);
		bool HasScript(const char* scriptName);
		int GetScriptReference(const char* scriptName) const;

		std::shared_ptr<Pex::Binary> GetScript(const char* scriptName);
		bool GetDecompiledSource(const char* scriptName, std::string& decompiledSource);
		bool GetSourceData(const char* scriptName, dap::Source& data);
	private:
		std::mutex m_scriptsMutex;
		std::map<int, std::shared_ptr<Pex::Binary>> m_scripts;
	};
}
