// Copyright (c) 2017 Samsung Electronics Co., LTD
// Distributed under the MIT License.
// See the LICENSE file in the project root for more information.

#pragma once

#include <string>
#include <vector>



// From https://github.com/Microsoft/vscode-debugadapter-node/blob/master/protocol/src/debugProtocol.ts

struct Thread
{
	int id;
	std::string name;
	bool running;

	Thread() {}
	Thread(int id, std::string name, bool running) : id(id), name(name), running(running) {}
};

struct Source
{
	std::string name;
	std::string path;
	int sourceReference;

	Source(std::string name = std::string(), std::string path = std::string(), int sourceReference = 0) : name(name), path(path), sourceReference(sourceReference) {}
	bool IsNull() const { return name.empty() && path.empty() && sourceReference == 0; }
};


struct StackFrame
{
	uint32_t id; // (threadId << 32) | level
	std::string name;
	Source source;
	int line;
	int column;
	int endLine;
	int endColumn;
	std::string moduleId;

	StackFrame() :
		id(0), line(0), column(0), endLine(0), endColumn(0) {}

	StackFrame(uint64_t id) : id(id), line(0), column(0), endLine(0), endColumn(0) {}
};

struct Breakpoint
{
	bool verified;
	std::string message;
	Source source;
	int line;

	std::string condition;
	std::string module;
	std::string params;

	Breakpoint() : verified(false), line(0) {}
};

enum SymbolStatus
{
	SymbolsSkipped, // "Skipped loading symbols."
	SymbolsLoaded,  // "Symbols loaded." 
	SymbolsNotFound
};

struct Module
{
	std::string id;
	std::string name;
	std::string path;
	// bool isOptimized; // TODO: support both fields for VSCode protocol
	// bool isUserCode;
	SymbolStatus symbolStatus;
	uint64_t baseAddress; // exposed for MI protocol
	uint32_t size; // exposed for MI protocol

	Module() : symbolStatus(SymbolsSkipped), baseAddress(0), size(0) {}
};

enum BreakpointReason
{
	BreakpointChanged,
	BreakpointNew,
	BreakpointRemoved
};

enum StopReason
{
	StopStep,
	StopBreakpoint,
	StopException,
	StopPause,
	StopEntry
};

struct StoppedEvent
{
	StopReason reason;
	std::string description;
	int threadId;
	std::string text;
	bool allThreadsStopped;

	StackFrame frame; // exposed for MI protocol
	Breakpoint breakpoint; // exposed for MI protocol

	StoppedEvent(StopReason reason, int threadId = 0) : reason(reason), threadId(threadId), allThreadsStopped(true) {}
};

struct ContinuedEvent
{
	int threadId;
	bool allThreadsContinued;

	ContinuedEvent(int threadId = 0) : threadId(threadId), allThreadsContinued(true) {}
};

struct BreakpointEvent
{
	BreakpointReason reason;
	Breakpoint breakpoint;

	BreakpointEvent(BreakpointReason reason, Breakpoint breakpoint) : reason(reason), breakpoint(breakpoint) {}
};

struct ExitedEvent
{
	int exitCode;

	ExitedEvent(int exitCode) : exitCode(exitCode) {}
};

enum ThreadReason
{
	ThreadStarted,
	ThreadExited
};

struct ThreadEvent
{
	ThreadReason reason;
	int threadId;

	ThreadEvent(ThreadReason reason, int threadId) : reason(reason), threadId(threadId) {}
};

enum OutputCategory
{
	OutputConsole,
	OutputStdOut,
	OutputStdErr
};

struct OutputEvent
{
	OutputCategory category;
	std::string output;

	std::string source; // exposed for MI protocol

	OutputEvent(OutputCategory category, std::string output) : category(category), output(output) {}
};

enum ModuleReason
{
	ModuleNew,
	ModuleChanged,
	ModuleRemoved
};

struct ModuleEvent
{
	ModuleReason reason;
	Module module;
	ModuleEvent(ModuleReason reason, const Module &module) : reason(reason), module(module) {}
};

enum LoadedSourceReason
{
	SourceNew,
	SourceChanged,
	SourceRemoved
};

struct LoadedSourceEvent
{
	LoadedSourceReason reason;
	Source source;
	LoadedSourceEvent(LoadedSourceReason reason, const Source& source) : reason(reason), source(source) {}
};

struct Scope
{
	std::string name;
	uint64_t variablesReference;
	int namedVariables;
	int indexedVariables;
	bool expensive;

	Scope() : variablesReference(0), namedVariables(0), expensive(false) {}

	Scope(uint64_t variablesReference, const std::string &name, int namedVariables) :
		name(name),
		variablesReference(variablesReference),
		namedVariables(namedVariables),
		indexedVariables(0),
		expensive(false)
	{}
};


// TODO: Replace strings with enums
struct VariablePresentationHint
{
	std::string kind;
	std::vector<std::string> attributes;
	std::string visibility;
};

struct Variable
{
	std::string name;
	std::string value;
	std::string type;
	VariablePresentationHint presentationHint;
	std::string evaluateName;
	uint32_t variablesReference;
	int namedVariables;
	int indexedVariables;

	Variable() : variablesReference(0), namedVariables(0), indexedVariables(0) {}
};

enum VariablesFilter
{
	VariablesNamed,
	VariablesIndexed,
	VariablesBoth
};

struct SourceBreakpoint
{
	int line;
	std::string condition;

	SourceBreakpoint(int linenum, const std::string &cond = std::string()) : line(linenum), condition(cond) {}
};

struct FunctionBreakpoint
{
	std::string module;
	std::string func;
	std::string params;
	std::string condition;

	FunctionBreakpoint(const std::string &module,
					   const std::string &func,
					   const std::string &params,
					   const std::string &cond = std::string()) :
		module(module),
		func(func),
		params(params),
		condition(cond)
	{}
};
