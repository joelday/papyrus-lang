#pragma once

#include <Champollion/Pex/Binary.hpp>

namespace DarkId::Papyrus::DebugServer
{
    bool ReadPexResource(const std::string& scriptName, std::ostream& stream);
    bool LoadAndDumpPexData(const std::string& scriptName, std::string outputDir);
    bool LoadPexData(const std::string& scriptName, Pex::Binary& binary);
	Pex::Function* GetFunctionData(std::shared_ptr<Pex::Binary> binary, Pex::StringTable::Index objName, Pex::StringTable::Index stateName, Pex::StringTable::Index funcName);

}
