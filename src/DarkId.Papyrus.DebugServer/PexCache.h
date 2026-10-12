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
		bool HasScript(const std::string & scriptName);

		std::shared_ptr<Pex::Binary> GetCachedScript(const int ref);

		std::shared_ptr<Pex::Binary> GetScript(const std::string & scriptName);
		bool GetDecompiledSource(const std::string & scriptName, std::string& decompiledSource);
		bool GetSourceData(const std::string &scriptName, dap::Source& data);
		void Clear();
	private:
		std::mutex m_scriptsMutex;
		std::map<int, std::shared_ptr<Pex::Binary>> m_scripts;
	};
}
