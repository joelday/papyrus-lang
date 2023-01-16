#pragma once

#include <Champollion/Pex/Binary.hpp>

namespace DarkId::Papyrus::DebugServer
{
    bool LoadAndDumpPexData(const char* scriptName, std::string outputDir);
    bool LoadPexData(const char* scriptName, Pex::Binary& binary);
}
