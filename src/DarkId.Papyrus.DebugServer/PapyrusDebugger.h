#pragma once

#include "Protocol/debugger.h"

#include "RuntimeEvents.h"
#include "PexCache.h"
#include "BreakpointManager.h"
#include "DebugExecutionManager.h"
#include "IdMap.h"
#include <forward_list>

namespace DarkId::Papyrus::DebugServer
{
	class PapyrusDebugger :
		public Debugger
	{
	public:
		PapyrusDebugger(Protocol* protocol);
		~PapyrusDebugger();

		bool IsJustMyCode() const { return false; };
		void SetJustMyCode(bool enable) { };

		HRESULT Initialize() override;
		HRESULT Attach() override { return 0; };
		HRESULT Launch(std::string fileExec, std::vector<std::string> execArgs, bool stopAtEntry = false) override { return 0; }
		HRESULT ConfigurationDone() override { return 0; }

		HRESULT Disconnect(DisconnectAction action = DisconnectDefault) override { return 0; }

		int GetLastStoppedThreadId() override { return 0; }

		HRESULT Continue() override;
		HRESULT Pause() override;
		HRESULT GetThreads(std::vector<Thread>& threads) override;
		HRESULT SetBreakpoints(Source& source, const std::vector<SourceBreakpoint>& srcBreakpoints, std::vector<Breakpoint>& breakpoints) override;
		HRESULT SetFunctionBreakpoints(const std::vector<FunctionBreakpoint>& funcBreakpoints, std::vector<Breakpoint>& breakpoints) override { return 0; }
		void InsertExceptionBreakpoint(const std::string& name, Breakpoint& breakpoint) override { }
		HRESULT GetStackTrace(int threadId, int startFrame, int levels, std::vector<StackFrame>& stackFrames, int& totalFrames) override;
		HRESULT StepCommand(int threadId, StepType stepType) override;
		HRESULT GetScopes(uint64_t frameId, std::vector<Scope>& scopes) override;
		HRESULT GetVariables(uint64_t variablesReference, VariablesFilter filter, int start, int count, std::vector<Variable>& variables) override;
		int GetNamedVariables(uint64_t variablesReference) override;
		HRESULT Evaluate(uint64_t frameId, const std::string& expression, Variable& variable, std::string& output) override { return 0; }
		HRESULT SetVariable(const std::string& name, const std::string& value, uint32_t ref, std::string& output) override { return 0; }
		HRESULT SetVariableByExpression(uint64_t frameId, const std::string& name, const std::string& value, std::string& output) override { return 0; }
		HRESULT GetSource(Source& source, std::string& output) override;
		HRESULT GetLoadedSources(std::vector<Source>& sources) override;
	private:
		bool m_closed = false;

		std::shared_ptr<IdProvider> m_idProvider;

		Protocol* m_protocol;
		std::shared_ptr<PexCache> m_pexCache;
		std::shared_ptr<BreakpointManager> m_breakpointManager;
		std::shared_ptr<RuntimeState> m_runtimeState;
		std::shared_ptr<DebugExecutionManager> m_executionManager;

		std::mutex m_instructionMutex;

		RuntimeEvents::CreateStackEventHandle m_createStackEventHandle;
		RuntimeEvents::CleanupStackEventHandle m_cleanupStackEventHandle;
		RuntimeEvents::InstructionExecutionEventHandle m_instructionExecutionEventHandle;
		// RuntimeEvents::InitScriptEventHandle m_initScriptEventHandle;
		RuntimeEvents::LogEventHandle m_logEventHandle;

		// void InitScriptEvent(RE::TESInitScriptEvent* initEvent);
		void EventLogged(const RE::BSScript::LogEvent* logEvent) const;
		void StackCreated(RE::BSTSmartPointer<RE::BSScript::Stack>& stack);
		void StackCleanedUp(uint32_t stackId);
		void InstructionExecution(CodeTasklet* tasklet, CodeTasklet::OpCode opCode) const;
		void CheckSourceLoaded(const char* scriptName) const;
	};
}
