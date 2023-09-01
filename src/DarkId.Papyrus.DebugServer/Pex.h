#pragma once

#include <Champollion/Pex/Binary.hpp>

namespace DarkId::Papyrus::DebugServer
{
    bool ReadPexResource(const std::string& scriptName, std::ostream& stream);
    bool LoadAndDumpPexData(const std::string& scriptName, std::string outputDir);
    bool LoadPexData(const std::string& scriptName, Pex::Binary& binary);
}
