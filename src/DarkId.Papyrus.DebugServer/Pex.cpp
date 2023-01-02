#include "Pex.h"

#include <sstream>
#include <regex>

#include "GameInterfaces.h"
#include "Utilities.h"
#if SKYRIM
#include <SKSE/Logger.h>
#elif FALLOUT
#include <F4SE/Logger.h>
#endif 

#include <Champollion/Pex/FileReader.hpp>

namespace DarkId::Papyrus::DebugServer
{
	bool ReadPexResource(const char* scriptName, std::ostream& stream)
	{
		auto scriptPath = "Scripts/" + ScriptNameToPEXPath(scriptName);
		RE::BSResourceNiBinaryStream scriptStream(scriptPath);
		bool good = false;
		if (scriptStream.good())
		{
			char byte;
			while (scriptStream.get(byte))
			{
				stream.put(byte);
			}
			good = true;
		} 
		#if FALLOUT
		else {
			// BSResourceNiBinaryStream(scriptPath) doesn't pick up loose PEX files in Fallout, have to use this to do so.
			auto rescanStream = RE::BSResourceNiBinaryStream::BinaryStreamWithRescan(scriptPath.c_str());
			//if (!rescanStream->good()) {
			//	// Creation Club?
			//	delete rescanStream;
			//	scriptPath = std::string(NormalizeScriptName(scriptName) + ".pex");
			//	rescanStream = RE::BSResourceNiBinaryStream::BinaryStreamWithRescan(scriptPath.c_str());
			//}
			if (rescanStream->good())
			{
				char byte;
				while (rescanStream->get(byte))
				{
					stream.put(byte);
				}
				good = true;
			}
			delete rescanStream;
		}
		#endif
		return good;
	}

	bool LoadAndDumpPexData(const char* scriptName, std::string outputDir) {
		std::stringstream buffer;

		if (!ReadPexResource(scriptName, buffer))
		{
			logger::error("Failed to load pex resource for script {}"sv, scriptName);
			return false;
		}

		buffer.seekp(0);
		// DEBUG
		std::string scriptFile(scriptName);
		auto outputPath = std::filesystem::path(outputDir) / (std::string(scriptName) + ".pex");
		std::ofstream output(outputPath, std::ios::binary);
		if (output.bad()) {
			logger::error("Failed to open file for writing: {}"sv, outputPath.string());
			return false;

		}
		char byte;
		while (buffer.get(byte)) {
			output.put(byte);
		}
		output.close();
		return true;
	}

	bool LoadPexData(const char* scriptName, Pex::Binary& binary)
	{
		std::stringstream buffer;

		if (!ReadPexResource(scriptName, buffer))
		{
			logger::error("Failed to load pex resource for script {}"sv, scriptName);
			return false;
		}

		buffer.seekp(0);

		std::istream input(buffer.rdbuf());
		Pex::FileReader reader(&input);
		try {
			reader.read(binary);
		}
		catch (std::runtime_error e) {
			logger::error("Failed to parse PEX resource {}:"sv, scriptName);
			logger::error("\t{}"sv, e.what());
		}
		return true;
	}
}
