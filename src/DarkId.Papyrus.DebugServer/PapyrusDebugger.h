#pragma once

#include <dap/protocol.h>
#include <dap/session.h>
#include <dap/traits.h>
#include "RuntimeEvents.h"
#include "PexCache.h"
#include "BreakpointManager.h"
#include "DebugExecutionManager.h"
#include "IdMap.h"
#include <forward_list>
#include <Protocol/struct_extensions.h>

namespace DarkId::Papyrus::DebugServer
{
	enum DisconnectAction
	{
		DisconnectDefault, // Attach -> Detach, Launch -> Terminate
		DisconnectTerminate,
		DisconnectDetach
	};


	enum VariablesFilter
	{
		VariablesNamed,
		VariablesIndexed,
		VariablesBoth
	};
	class PapyrusDebugger
	{
		template <typename T>
		using IsEvent = dap::traits::EnableIfIsType<dap::Event, T>;

	public:
		PapyrusDebugger();
		~PapyrusDebugger();
		void StartSession(std::shared_ptr<dap::Session> session);
		void EndSession();
		bool IsJustMyCode() const { return false; };
		void SetJustMyCode(bool enable) { };
		template <typename T, typename = IsEvent<T>>
		void SendEvent(const T& event) const;
		void LogGameOutput(RE::BSScript::ErrorLogger::Severity severity, const std::string& msg) const;		
		void Initialize();
		//HRESULT Initialize() ;
		// HRESULT Attach()  { return 0; };
		// HRESULT Launch(std::string fileExec, std::vector<std::string> execArgs, bool stopAtEntry = false)  { return 0; }
		// HRESULT ConfigurationDone()  { return 0; }

		// dap::Response Disconnect(DisconnectAction action = DisconnectDefault)  { return dap::DisconnectResponse(); }

		int GetLastStoppedThreadId()  { return 0; }
		dap::ResponseOrError<dap::InitializeResponse> Initialize(const dap::InitializeRequest& request);
		dap::ResponseOrError<dap::LaunchResponse> Launch(const dap::PDSLaunchRequest& request);
		dap::ResponseOrError<dap::AttachResponse> Attach(const dap::PDSAttachRequest& request);
		dap::ResponseOrError<dap::ContinueResponse> Continue(const dap::ContinueRequest& request) ;
		dap::ResponseOrError<dap::PauseResponse> Pause(const dap::PauseRequest& request) ;
		dap::ResponseOrError<dap::ThreadsResponse> GetThreads(const dap::ThreadsRequest& request) ;
		dap::ResponseOrError<dap::SetBreakpointsResponse> SetBreakpoints(const dap::SetBreakpointsRequest& request) ;
		dap::ResponseOrError<dap::SetFunctionBreakpointsResponse> SetFunctionBreakpoints(const dap::SetFunctionBreakpointsRequest& request);
		dap::ResponseOrError<dap::StackTraceResponse> GetStackTrace(const dap::StackTraceRequest& request) ;
		dap::ResponseOrError<dap::StepInResponse> StepIn(const dap::StepInRequest& request);
		dap::ResponseOrError<dap::StepOutResponse> StepOut(const dap::StepOutRequest& request);
		dap::ResponseOrError<dap::NextResponse> Next(const dap::NextRequest& request);
		dap::ResponseOrError<dap::ScopesResponse> GetScopes(const dap::ScopesRequest& request) ;
		dap::ResponseOrError<dap::VariablesResponse> GetVariables(const dap::VariablesRequest& request);
		dap::ResponseOrError<dap::SourceResponse> GetSource(const dap::SourceRequest& request);
		dap::ResponseOrError<dap::LoadedSourcesResponse> GetLoadedSources(const dap::LoadedSourcesRequest& request);
		// dap::Response Evaluate(const dap::SetBreakpointsRequest& request)  { return 0; }
		// dap::Response SetVariable(const dap::SetBreakpointsRequest& request)  { return 0; }
		// dap::Response SetVariableByExpression(const dap::SetBreakpointsRequest& request)  { return 0; }
		// void InsertExceptionBreakpoint(const std::string& name, dap::Breakpoint& breakpoint)  { }
		// int GetNamedVariables(uint64_t variablesReference) ;
	//	void bind(const std::shared_ptr<dap::Session>& session);
	private:
		bool m_closed = false;

		std::atomic<uint64_t> msg_counter = 0;
		std::shared_ptr<IdProvider> m_idProvider;

		std::shared_ptr<dap::Session> m_session;
		std::shared_ptr<PexCache> m_pexCache;
		std::shared_ptr<BreakpointManager> m_breakpointManager;
		std::shared_ptr<RuntimeState> m_runtimeState;
		std::shared_ptr<DebugExecutionManager> m_executionManager;
		std::map<int, dap::Source> m_projectSources;
		std::string m_projectPath;
		std::string m_modDirectory;
		std::mutex m_instructionMutex;

		RuntimeEvents::CreateStackEventHandle m_createStackEventHandle;
		RuntimeEvents::CleanupStackEventHandle m_cleanupStackEventHandle;
		RuntimeEvents::InstructionExecutionEventHandle m_instructionExecutionEventHandle;
		// RuntimeEvents::InitScriptEventHandle m_initScriptEventHandle;
		RuntimeEvents::LogEventHandle m_logEventHandle;
		
		void RegisterSessionHandlers();

		// void InitScriptEvent(RE::TESInitScriptEvent* initEvent);
		void EventLogged(const RE::BSScript::LogEvent* logEvent) const;
		void StackCreated(RE::BSTSmartPointer<RE::BSScript::Stack>& stack);
		void StackCleanedUp(uint32_t stackId);
		void InstructionExecution(CodeTasklet* tasklet, CodeTasklet::OpCode opCode) const;
		void CheckSourceLoaded(const std::string &scriptName) const;
};
}
