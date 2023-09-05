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
	bool ReadPexResource(const std::string& scriptName, std::ostream& stream)
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


	bool LoadAndDumpPexData(const std::string& scriptName, std::string outputDir) {
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
		Pex::Binary thing;
		auto athing = LoadPexData(scriptName, thing);
		return true;
	}

	bool LoadPexData(const std::string& scriptName, Pex::Binary& binary)
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

	Pex::Function* GetFunctionData(std::shared_ptr<Pex::Binary> binary, Pex::StringTable::Index objName, Pex::StringTable::Index stateName, Pex::StringTable::Index funcName)
	{
		for (auto& object : binary->getObjects()) {
			if (object.getName() == objName) {
				for (auto& state : object.getStates()) {
					if (state.getName() == stateName) {
						for (auto& function : state.getFunctions()) {
							if (function.getName() == funcName) {
								return std::addressof(function);
							}
						}
					}
				}
			}
		}
		return nullptr;
	}

	bool OpCodeWillCallOrReturn(Pex::OpCode opcode) {
		switch (opcode) {
			case Pex::OpCode::CALLMETHOD:
			case Pex::OpCode::CALLPARENT:
			case Pex::OpCode::CALLSTATIC:
			case Pex::OpCode::RETURN:
			case Pex::OpCode::PROPGET:
			case Pex::OpCode::PROPSET:
				return true;
			default:
				return false;
		}
	}
}
