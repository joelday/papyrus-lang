// Copyright (c) 2018 Samsung Electronics Co., LTD
// Distributed under the MIT License.
// See the LICENSE file in the project root for more information.

#include "vscodeprotocol.h"

#include <iostream>
#include <unordered_map>

#include "torelease.h"
#include "cputil.h"
#include <iomanip>
#include <sstream>

#include "PDError.h"
// for convenience
using json = nlohmann::json;
#if SKYRIM
#include <SKSE/Impl/PCH.h>
#include <SKSE/Logger.h>
namespace XSE = SKSE;
namespace logger = SKSE::log;
#elif FALLOUT
namespace XSE = F4SE;
namespace logger = F4SE::log;
#endif

void from_json(const nlohmann::json& value, Source& source)
{
	source.name = value.value("name", std::string());
	source.path = value.value("path", std::string());

	if (value.find("sourceReference") != value.end())
	{
		source.sourceReference = value["sourceReference"];
	}
}

void to_json(json &j, const Source &s) {
	j = json{{"name", s.name},
			 {"path", s.path},
			 {"sourceReference", s.sourceReference}};
}

void to_json(json &j, const Breakpoint &b) {
	j = json{
		{"line",	 b.line},
		{"verified", b.verified},
		{"message",  b.message}};

	if (!b.source.IsNull())
		j["source"] = b.source;
}

void to_json(json &j, const StackFrame &f) {
	j = json{
		{"id",		f.id},
		{"name",	  f.name},
		{"line",	  f.line},
		{"column",	f.column},
		{"endLine",   f.endLine},
		{"endColumn", f.endColumn},
		{"moduleId",  f.moduleId}};
	if (!f.source.IsNull())
		j["source"] = f.source;
}

void to_json(json &j, const Thread &t) {
	j = json{{"id",   t.id},
			 {"name", t.name}};
		  // {"running", t.running}
}

void to_json(json &j, const Scope &s) {
	j = json{
		{"name",			   s.name},
		{"variablesReference", s.variablesReference}};

	if (s.variablesReference > 0)
	{
		j["namedVariables"] = s.namedVariables;
		// j["indexedVariables"] = s.indexedVariables;
	}
}

void to_json(json &j, const Variable &v) {
	j = json{
		{"name",			   v.name},
		{"value",			  v.value},
		{"type",			   v.type},
		{"evaluateName",	   v.evaluateName},
		{"variablesReference", v.variablesReference}};

	if (v.variablesReference > 0)
	{
		j["namedVariables"] = v.namedVariables;
		// j["indexedVariables"] = v.indexedVariables;
	}
}
void VSCodeProtocol::EmitStoppedEvent(StoppedEvent event)
{
	json body;

	switch(event.reason)
	{
		case StopStep:
			body["reason"] = "step";
			break;
		case StopBreakpoint:
			body["reason"] = "breakpoint";
			break;
		case StopException:
			body["reason"] = "exception";
			break;
		case StopPause:
			body["reason"] = "pause";
			break;
		case StopEntry:
			body["reason"] = "entry";
			break;
	}

	body["description"] = event.description;
	body["text"] = event.text;
	body["threadId"] = event.threadId;
	body["allThreadsStopped"] = event.allThreadsStopped;

	// vsdbg shows additional info, but it is not a part of the protocol
	// body["line"] = event.frame.line;
	// body["column"] = event.frame.column;

	// body["source"] = event.frame.source;

	EmitEvent("stopped", body);
}

void VSCodeProtocol::EmitContinuedEvent(ContinuedEvent event)
{
	json body;

	body["threadId"] = event.threadId;
	body["allThreadsContinued"] = event.allThreadsContinued;

	EmitEvent("continued", body);
}

void VSCodeProtocol::EmitExitedEvent(ExitedEvent event)
{
	json body;
	body["exitCode"] = event.exitCode;
	EmitEvent("exited", body);
}

void VSCodeProtocol::EmitTerminatedEvent()
{
	EmitEvent("terminated", json::object());
}

void VSCodeProtocol::EmitThreadEvent(ThreadEvent event)
{
	json body;

	switch(event.reason)
	{
		case ThreadStarted:
			body["reason"] = "started";
			break;
		case ThreadExited:
			body["reason"] = "exited";
			break;
	}

	body["threadId"] = event.threadId;

	EmitEvent("thread", body);
}

void VSCodeProtocol::EmitModuleEvent(ModuleEvent event)
{
	json body;

	switch(event.reason)
	{
		case ModuleNew:
			body["reason"] = "new";
			break;
		case ModuleChanged:
			body["reason"] = "changed";
			break;
		case ModuleRemoved:
			body["reason"] = "removed";
			break;
	}

	json &module = body["module"];
	module["id"] = event.module.id;
	module["name"] = event.module.name;
	module["path"] = event.module.path;

	switch(event.module.symbolStatus)
	{
		case SymbolsSkipped:
			module["symbolStatus"] = "Skipped loading symbols.";
			break;
		case SymbolsLoaded:
			module["symbolStatus"] = "Symbols loaded.";
			break;
		case SymbolsNotFound:
			module["symbolStatus"] = "Symbols not found.";
			break;
	}

	EmitEvent("module", body);
}

void VSCodeProtocol::EmitLoadedSourceEvent(LoadedSourceEvent event)
{
	json body;

	switch (event.reason)
	{
	case ModuleNew:
		body["reason"] = "new";
		break;
	case ModuleChanged:
		body["reason"] = "changed";
		break;
	case ModuleRemoved:
		body["reason"] = "removed";
		break;
	}

	body["source"] = event.source;

	EmitEvent("loadedSource", body);
}

void VSCodeProtocol::EmitOutputEvent(OutputEvent event)
{
	json body;

	switch(event.category)
	{
		case OutputConsole:
			body["category"] = "console";
			break;
		case OutputStdOut:
			body["category"] = "stdout";
			break;
		case OutputStdErr:
			body["category"] = "stderr";
			break;
	}

	body["output"] = event.output;

	EmitEvent("output", body);
}

void VSCodeProtocol::EmitBreakpointEvent(BreakpointEvent event)
{
	json body;

	switch(event.reason)
	{
		case BreakpointNew:
			body["reason"] = "new";
			break;
		case BreakpointChanged:
			body["reason"] = "changed";
			break;
		case BreakpointRemoved:
			body["reason"] = "removed";
			break;
	}

	body["breakpoint"] = event.breakpoint;

	EmitEvent("breakpoint", body);
}

void VSCodeProtocol::EmitInitializedEvent()
{
	EmitEvent("initialized", json::object());
}

void VSCodeProtocol::EmitCapabilitiesEvent()
{
	json body = json::object();
	json capabilities = json::object();

	AddCapabilitiesTo(capabilities);

	body["capabilities"] = capabilities;

	EmitEvent("capabilities", body);
}

void VSCodeProtocol::Cleanup()
{

}

void VSCodeProtocol::EmitEvent(const std::string &name, const nlohmann::json &body)
{
	if (m_exit)
	{
		return;
	}

	std::lock_guard<std::mutex> lock(m_outMutex);
	json response;
	response["seq"] = m_seqCounter++;
	response["type"] = "event";
	response["event"] = name;
	response["body"] = body;

	std::string output = response.dump();

	m_sendCallback(output);

	Log(LOG_EVENT, output);
}

typedef std::function<HRESULT(
	const json &arguments,
	json &body)> CommandCallback;

void VSCodeProtocol::AddCapabilitiesTo(json &capabilities)
{
	capabilities["supportsConfigurationDoneRequest"] = true;
	capabilities["supportsLoadedSourcesRequest"] = true;
	// capabilities["supportsFunctionBreakpoints"] = true;
	// capabilities["supportsConditionalBreakpoints"] = true;
	// capabilities["supportTerminateDebuggee"] = true;
}

HRESULT VSCodeProtocol::HandleCommand(const std::string &command, const json &arguments, json &body)
{
	std::unordered_map<std::string, CommandCallback> commands {
	{ "initialize", [this](const json &arguments, json &body){

		EmitCapabilitiesEvent();

		m_debugger->Initialize();
		AddCapabilitiesTo(body);

		return S_OK;
	} },
	{ "configurationDone", [this](const json &arguments, json &body){
		return m_debugger->ConfigurationDone();
	} },
	{ "setBreakpoints", [this](const json &arguments, json &body){
		HRESULT Status;

		std::vector<SourceBreakpoint> srcBreakpoints;
		for (auto &b : arguments.at("breakpoints"))
			srcBreakpoints.emplace_back(b.at("line"), b.value("condition", std::string()));

		std::vector<Breakpoint> breakpoints;

		Source source = arguments.at("source");
		Status = m_debugger->SetBreakpoints(source, srcBreakpoints, breakpoints);
		if (FAILED(Status)) {
			if (Status == DarkId::Papyrus::DebugServer::PDError::NO_DEBUG_INFO) {
				body["breakpoints"] = breakpoints;
				body["messasge"] = std::string("Debug info for " + source.name + " not present in PEX data, ensure script is compiled with Debug and that the game is configured to load papyrus debug info");
				return Status;
			}
			else if (Status == DarkId::Papyrus::DebugServer::PDError::NO_PEX_DATA) {
				body["breakpoints"] = breakpoints;
				body["messasge"] = std::string("Could not locate PEX data for " + source.name + ", ensure that it is loaded.");
				return Status;
			}
			else {
				body["messasge"] = std::string("setBreakpoints failed for " + source.name);
				return Status;
			}
		}

		body["breakpoints"] = breakpoints;

		return S_OK;
	} },
	{ "launch", [this](const json &arguments, json &body){
		if (!m_fileExec.empty())
		{
			return m_debugger->Launch(m_fileExec, m_execArgs, arguments.value("stopAtEntry", false));
		}
		std::vector<std::string> args = arguments.value("args", std::vector<std::string>());
		args.insert(args.begin(), arguments.at("program").get<std::string>());
		return m_debugger->Launch("dotnet", args, arguments.value("stopAtEntry", false));
	} },
	{ "threads", [this](const json &arguments, json &body){
		HRESULT Status;
		std::vector<Thread> threads;
		IfFailRet(m_debugger->GetThreads(threads));

		body["threads"] = threads;

		return S_OK;
	} },
	{ "disconnect", [this](const json &arguments, json &body){
		auto terminateArgIter = arguments.find("terminateDebuggee");
		Debugger::DisconnectAction action;
		if (terminateArgIter == arguments.end())
			action = Debugger::DisconnectDefault;
		else
			action = terminateArgIter.value().get<bool>() ? Debugger::DisconnectTerminate : Debugger::DisconnectDetach;

		m_debugger->Disconnect(action);

		m_exit = true;
		return S_OK;
	} },
	{ "stackTrace", [this](const json &arguments, json &body){
		HRESULT Status;

		int totalFrames = 0;
		int threadId = arguments.at("threadId");

		std::vector<StackFrame> stackFrames;
		IfFailRet(m_debugger->GetStackTrace(
			threadId,
			arguments.value("startFrame", 0),
			arguments.value("levels", 0),
			stackFrames,
			totalFrames
			));

		body["stackFrames"] = stackFrames;
		body["totalFrames"] = totalFrames;

		return S_OK;
	} },
	{ "continue", [this](const json &arguments, json &body){
		return m_debugger->Continue();
	} },
	{ "pause", [this](const json &arguments, json &body){
		return m_debugger->Pause();
	} },
	{ "next", [this](const json &arguments, json &body){
		return m_debugger->StepCommand(arguments.at("threadId"), Debugger::STEP_OVER);
	} },
	{ "stepIn", [this](const json &arguments, json &body){
		return m_debugger->StepCommand(arguments.at("threadId"), Debugger::STEP_IN);
	} },
	{ "stepOut", [this](const json &arguments, json &body){
		return m_debugger->StepCommand(arguments.at("threadId"), Debugger::STEP_OUT);
	} },
	{ "scopes", [this](const json &arguments, json &body){
		HRESULT Status;
		std::vector<Scope> scopes;
		IfFailRet(m_debugger->GetScopes(arguments.at("frameId"), scopes));

		body["scopes"] = scopes;

		return S_OK;
	} },
	{ "variables", [this](const json &arguments, json &body){
		HRESULT Status;

	   std::string filterName = arguments.value("filter", "");
		VariablesFilter filter = VariablesBoth;
		if (filterName == "named")
			filter = VariablesNamed;
		else if (filterName == "indexed")
			filter = VariablesIndexed;

		std::vector<Variable> variables;
		IfFailRet(m_debugger->GetVariables(
			arguments.at("variablesReference"),
			filter,
			arguments.value("start", 0),
			arguments.value("count", 0),
			variables));

		body["variables"] = variables;

		return S_OK;
	} },
	{ "evaluate", [this](const json &arguments, json &body){
		HRESULT Status;
		std::string expression = arguments.at("expression");
		uint64_t frameId;
		auto frameIdIter = arguments.find("frameId");
		if (frameIdIter == arguments.end())
		{
			// int threadId = m_debugger->GetLastStoppedThreadId();
			frameId = -1;
		}
		else 
			frameId = frameIdIter.value();

		Variable variable;
		std::string output;
		Status = m_debugger->Evaluate(frameId, expression, variable, output);
		if (FAILED(Status))
		{
			body["message"] = output;
			return Status;
		}

		body["result"] = variable.value;
		body["type"] = variable.type;
		body["variablesReference"] = variable.variablesReference;
		if (variable.variablesReference > 0)
		{
			body["namedVariables"] = variable.namedVariables;
			// indexedVariables
		}
		return S_OK;
	} },
	{ "attach", [this](const json &arguments, json &body){
		return m_debugger->Attach();
	} },
	{ "setVariable", [this](const json &arguments, json &body) {
		HRESULT Status;

		std::string name = arguments.at("name");
		std::string value = arguments.at("value");
		int ref = arguments.at("variablesReference");

		std::string output;
		Status = m_debugger->SetVariable(name, value, ref, output);
		if (FAILED(Status))
		{
			body["message"] = output;
			return Status;
		}

		body["value"] = output;

		return S_OK;
	} },
	{ "source", [this](const json & arguments, json & body) {
		HRESULT Status;

		Source source(
			arguments["source"]["name"],
			arguments["source"]["path"],
			arguments["source"]["sourceReference"]);
		
		std::string content;
		Status = m_debugger->GetSource(source, content);

		if (FAILED(Status))
		{
			body["message"] = content;
			return Status;
		}

		body["content"] = content;

		return S_OK;
	} },
	{ "loadedSources", [this](const json & arguments, json & body) {
		HRESULT Status;

		std::vector<Source> sources;
		Status = m_debugger->GetLoadedSources(sources);

		if (FAILED(Status))
		{
			body["message"] = "Failed to get loaded sources.";
			return Status;
		}

		body["sources"] = sources;

		return S_OK;
} },
	{ "setFunctionBreakpoints", [this](const json &arguments, json &body) {
		HRESULT Status = S_OK;

		std::vector<FunctionBreakpoint> funcBreakpoints;
		for (auto &b : arguments.at("breakpoints"))
		{
			std::string module("");
			std::string params("");
			std::string name = b.at("name");

			std::size_t i = name.find('!');

			if (i != std::string::npos)
			{
				module = std::string(name, 0, i);
				name.erase(0, i + 1);
			}

			i = name.find('(');
			if (i != std::string::npos)
			{
				std::size_t closeBrace = name.find(')');

				params = std::string(name, i, closeBrace - i + 1);
				name.erase(i, closeBrace);
			}

			funcBreakpoints.emplace_back(module, name, params, b.value("condition", std::string()));
		}

		std::vector<Breakpoint> breakpoints;
		IfFailRet(m_debugger->SetFunctionBreakpoints(funcBreakpoints, breakpoints));

		body["breakpoints"] = breakpoints;

		return Status;
	} }
	};

	auto command_it = commands.find(command);
	if (command_it == commands.end())
	{
		return E_NOTIMPL;
	}

	return command_it->second(arguments, body);
}

void VSCodeProtocol::Exit()
{
	m_exit = true;
}

void VSCodeProtocol::Receive(std::string message)
{
	std::lock_guard<std::mutex> lock(m_inMutex);
	m_inputQueue->push(message);
}

void VSCodeProtocol::CommandLoop()
{
	while (!m_exit)
	{
		std::string requestText;

		while (!m_exit)
		{
			{
				std::lock_guard<std::mutex> lock(m_inMutex);

				if (!m_inputQueue->empty())
				{
					requestText = m_inputQueue->front();
					m_inputQueue->pop();
					break;
				}
			}

			Sleep(100);
		}

		if (requestText.empty())
			break;

		{
			std::lock_guard<std::mutex> lock(m_outMutex);
			Log(LOG_COMMAND, requestText);
		}

		json request = json::parse(requestText, NULL, false);

		if (request.find("command") == request.end())
		{
			continue;
		}

		std::string command = request.at("command");
		// assert(request["type"] == "request");

		auto argIter = request.find("arguments");
		json arguments = (argIter == request.end() ? json::object() : argIter.value());

		json body = json::object();
		HRESULT Status = HandleCommand(command, arguments, body);

		{
			std::lock_guard<std::mutex> lock(m_outMutex);

			json response;
			response["seq"] = m_seqCounter++;
			response["type"] = "response";
			response["command"] = command;
			response["request_seq"] = request.at("seq");

			if (SUCCEEDED(Status))
			{
				response["success"] = true;
				response["body"] = body;
			}
			else
			{
				if (body.find("message") == body.end())
				{
					std::ostringstream ss;
					ss << "Failed command '" << command << "' : "
					<< "0x" << std::setw(8) << std::setfill('0') << std::hex << Status;
					response["message"] = ss.str();
				}
				else
					response["message"] = body["message"];

				response["success"] = false;
			}
			std::string output = response.dump();

			if (!m_exit)
			{
				m_sendCallback(output);
			}
		}
	}

	if (!m_exit)
		m_debugger->Disconnect();

}

const std::string VSCodeProtocol::LOG_COMMAND("-> (C) ");
const std::string VSCodeProtocol::LOG_RESPONSE("<- (R) ");
const std::string VSCodeProtocol::LOG_EVENT("<- (E) ");

void VSCodeProtocol::Log(const std::string &prefix, const std::string &text)
{
	using namespace std::literals;
	logger::info("{}: {}"sv, prefix.c_str(), text.c_str());
}
