#include "PapyrusDebugger.h"

#include <functional>
#include <string>
#include <dap/protocol.h>
#include <dap/session.h>

#include "Utilities.h"
#include "GameInterfaces.h"
#include "StackStateNode.h"
#include "StackFrameStateNode.h"

#if SKYRIM
	#include <SKSE/Logger.h>
	#include <SKSE/API.h>
namespace XSE = SKSE;
#elif FALLOUT
	#include <F4SE/API.h>
	#include <F4SE/Logger.h>
namespace XSE = F4SE;
namespace RE{
	using BSSpinLockGuard = BSAutoLock<BSSpinLock, BSAutoLockDefaultPolicy>;
}
#endif

#include "StateNodeBase.h"


namespace DarkId::Papyrus::DebugServer
{
	PapyrusDebugger::PapyrusDebugger(const std::shared_ptr<dap::Session>& session):
		m_session(session)
	{
		m_pexCache = std::make_shared<PexCache>();

		m_breakpointManager = std::make_shared<BreakpointManager>(m_pexCache.get());

		m_idProvider = std::make_shared<IdProvider>();
		m_runtimeState = std::make_shared<RuntimeState>(m_idProvider);

		m_executionManager = std::make_shared<DebugExecutionManager>(m_session, m_runtimeState.get(), m_breakpointManager.get());

		m_createStackEventHandle =
			RuntimeEvents::SubscribeToCreateStack(std::bind(&PapyrusDebugger::StackCreated, this, std::placeholders::_1));

		m_cleanupStackEventHandle =
			RuntimeEvents::SubscribeToCleanupStack(std::bind(&PapyrusDebugger::StackCleanedUp, this, std::placeholders::_1));

		m_instructionExecutionEventHandle =
			RuntimeEvents::SubscribeToInstructionExecution(
				std::bind(&PapyrusDebugger::InstructionExecution, this, std::placeholders::_1, std::placeholders::_2));

		// m_initScriptEventHandle = RuntimeEvents::SubscribeToInitScript(std::bind(&PapyrusDebugger::InitScriptEvent, this, std::placeholders::_1));

		m_logEventHandle =
			RuntimeEvents::SubscribeToLog(std::bind(&PapyrusDebugger::EventLogged, this, std::placeholders::_1));
		
	}

	void PapyrusDebugger::RegisterSessionHandlers(){
		m_session->registerHandler([&](const dap::PauseRequest& request) {
			return Pause(request);
		});
		m_session->registerHandler([&](const dap::ContinueRequest& request) {
			return Continue(request);
		});
		m_session->registerHandler([&](const dap::PauseRequest& request) {
			return Pause(request);
		});
		m_session->registerHandler([&](const dap::ThreadsRequest& request) {
			return GetThreads(request);
		});
		m_session->registerHandler([&](const dap::SetBreakpointsRequest& request) {
			return SetBreakpoints(request);
		});
		m_session->registerHandler([&](const dap::SetFunctionBreakpointsRequest& request) {
			return SetFunctionBreakpoints(request);
		});
		m_session->registerHandler([&](const dap::StackTraceRequest& request) {
			return GetStackTrace(request);
		});
		m_session->registerHandler([&](const dap::StepInRequest& request) {
			return StepIn(request);
		});
		m_session->registerHandler([&](const dap::StepOutRequest& request) {
			return StepOut(request);
		});
		m_session->registerHandler([&](const dap::NextRequest& request) {
			return Next(request);
		});
		m_session->registerHandler([&](const dap::ScopesRequest& request) {
			return GetScopes(request);
		});
		m_session->registerHandler([&](const dap::VariablesRequest& request) {
			return GetVariables(request);
		});
		m_session->registerHandler([&](const dap::SourceRequest& request) {
			return GetSource(request);
		});
		m_session->registerHandler([&](const dap::LoadedSourcesRequest& request) {
			return GetLoadedSources(request);
		});
	}
	std::string LogSeverityEnumStr(RE::BSScript::ErrorLogger::Severity severity) {
		if (severity == RE::BSScript::ErrorLogger::Severity::kInfo) {
			return std::string("INFO");
		} else if (severity == RE::BSScript::ErrorLogger::Severity::kWarning) {
			return std::string("WARNING");
		} else if (severity == RE::BSScript::ErrorLogger::Severity::kError) {
			return std::string("ERROR");
		} else if (severity == RE::BSScript::ErrorLogger::Severity::kFatal) {
			return std::string("FATAL");
		}
		return std::string("UNKNOWN_ENUM_LEVEL");
	}

	void PapyrusDebugger::EventLogged(const RE::BSScript::LogEvent* logEvent) const
	{
		const std::string severity = LogSeverityEnumStr(logEvent->severity);
		dap::OutputEvent output;
		output.category = "console";
#if SKYRIM
		const auto message = std::string(logEvent->errorMsg);
		output.output = message + "\r\n";
#elif FALLOUT
		RE::BSFixedString message;
		logEvent->errorMsg.GetErrorMsg(message);
		const auto msg = std::string(message.c_str());
		const auto ownerModule = std::string(logEvent->ownerModule.c_str());
		output.output = ownerModule + " - " + msg + "\r\n";
#endif
		m_session->send(output);
	}


	void PapyrusDebugger::StackCreated(RE::BSTSmartPointer<RE::BSScript::Stack>& stack)
	{
		const auto stackId = stack->stackID;
		
		XSE::GetTaskInterface()->AddTask([this, stackId]()
		{
			if (m_closed)
			{
				return;
			}

			const auto stack = m_runtimeState->GetStack(stackId);
			if (!stack)
			{
				return;
			}
			dap::ThreadEvent threadEvent;
			threadEvent.reason = "started";
			threadEvent.threadId = stackId;
			m_session->send(threadEvent);
			
			if (stack->top && stack->top->owningFunction)
			{
				// TODO: Not in use, just for debugging reference.
				auto srcFileName = stack->top->owningFunction->GetSourceFilename().c_str();
				auto scriptName = NormalizeScriptName(stack->top->owningObjectType->GetName());
				CheckSourceLoaded(scriptName.c_str());
			}
		});
	}
	
	void PapyrusDebugger::StackCleanedUp(uint32_t stackId)
	{
		XSE::GetTaskInterface()->AddTask([this, stackId]()
		{
			if (m_closed)
			{
				return;
			}
			dap::ThreadEvent threadEvent;
			threadEvent.reason = "exited";
			threadEvent.threadId = stackId;

			m_session->send(threadEvent);
		});
	}

	void PapyrusDebugger::InstructionExecution(CodeTasklet* tasklet, CodeTasklet::OpCode opcode) const
	{
		m_executionManager->HandleInstruction(tasklet, opcode);
	}

	void PapyrusDebugger::CheckSourceLoaded(const char* scriptName) const
	{
		if (!m_pexCache->HasScript(scriptName))
		{
			dap::Source source;
			if (!m_pexCache->GetSourceData(scriptName, source))
			{
				return;
			}
			dap::LoadedSourceEvent event;
			event.reason = "new";
			event.source = source;
			m_session->send(event);
		}
	}


	PapyrusDebugger::~PapyrusDebugger()
	{
		m_closed = true;

		RuntimeEvents::UnsubscribeFromLog(m_logEventHandle);
		// RuntimeEvents::UnsubscribeFromInitScript(m_initScriptEventHandle);
		RuntimeEvents::UnsubscribeFromInstructionExecution(m_instructionExecutionEventHandle);
		RuntimeEvents::UnsubscribeFromCreateStack(m_createStackEventHandle);
		RuntimeEvents::UnsubscribeFromCleanupStack(m_cleanupStackEventHandle);

		m_executionManager->Close();
	}
	dap::ResponseOrError<dap::ContinueResponse> PapyrusDebugger::Continue(const dap::ContinueRequest& request)
	{
		if (m_executionManager->Continue())
			dap::ContinueResponse();
		return dap::Error("Could not Continue");
	}
	dap::ResponseOrError<dap::PauseResponse> PapyrusDebugger::Pause(const dap::PauseRequest& request)
	{
		if (m_executionManager->Pause())
			dap::PauseResponse();
		return dap::Error("Could not Pause");
	}
	dap::ResponseOrError<dap::ThreadsResponse> PapyrusDebugger::GetThreads(const dap::ThreadsRequest& request)
	{
		dap::ThreadsResponse response;
		const auto vm = RE::BSScript::Internal::VirtualMachine::GetSingleton();
		RE::BSSpinLockGuard lock(vm->runningStacksLock);

		std::vector<std::string> stackIdPaths;

		for (auto& elem : vm->allRunningStacks)
		{
			const auto stack = elem.second.get();
			if (!stack || !stack->top)
			{
				continue;
			}

			stackIdPaths.push_back(std::to_string(stack->stackID));
		}

		for (auto& path : stackIdPaths)
		{
			std::shared_ptr<StateNodeBase> stateNode;
			if (!m_runtimeState->ResolveStateByPath(path, stateNode))
			{
				continue;
			}

			const auto node = dynamic_cast<StackStateNode*>(stateNode.get());

			dap::Thread thread;
			if (node->SerializeToProtocol(thread))
			{
				response.threads.push_back(thread);
			}
		}

		return response;
	}
	dap::ResponseOrError<dap::SetBreakpointsResponse> PapyrusDebugger::SetBreakpoints(const dap::SetBreakpointsRequest& request)
	{
		dap::SetBreakpointsResponse response;
		dap::Source source = request.source;
		return m_breakpointManager->SetBreakpoints(source, request.breakpoints.value(std::vector<dap::SourceBreakpoint>()));
	}

	dap::ResponseOrError<dap::SetFunctionBreakpointsResponse> PapyrusDebugger::SetFunctionBreakpoints(const dap::SetFunctionBreakpointsRequest& request)
	{
		return dap::Error("unimplemented");
	}
	dap::ResponseOrError<dap::StackTraceResponse> PapyrusDebugger::GetStackTrace(const dap::StackTraceRequest& request)
	{
		dap::StackTraceResponse response;
		const auto vm = RE::BSScript::Internal::VirtualMachine::GetSingleton();
		RE::BSSpinLockGuard lock(vm->runningStacksLock);

		if (request.threadId == -1)
		{
			response.totalFrames = 0;
			return dap::Error("No threadId specified");
		}

		std::vector<std::shared_ptr<StateNodeBase>> frameNodes;
		if (!m_runtimeState->ResolveChildrenByParentPath(std::to_string(request.threadId), frameNodes))
		{
			return dap::Error("Could not find ThreadId");
		}
		auto startFrame = request.startFrame.value(0);
		auto levels = request.levels.value(frameNodes.size());
		for (auto frameIndex = startFrame; frameIndex < frameNodes.size() && frameIndex < startFrame + levels; frameIndex++)
		{
			const auto node = dynamic_cast<StackFrameStateNode*>(frameNodes.at(frameIndex).get());

			dap::StackFrame frame;
			if (!node->SerializeToProtocol(frame, m_pexCache.get())) {
				return dap::Error("Serialization error");
			}

			response.stackFrames.push_back(frame);
		}
		return response;
	}
	dap::ResponseOrError<dap::StepInResponse> PapyrusDebugger::StepIn(const dap::StepInRequest& request)
	{
		// TODO: Support `granularity` and `target`
		if (m_executionManager->Step(request.threadId, STEP_IN)) {
			dap::StepInResponse();
		}
		return dap::Error("Could not StepIn");
	}
	dap::ResponseOrError<dap::StepOutResponse> PapyrusDebugger::StepOut(const dap::StepOutRequest& request)
	{
		if (m_executionManager->Step(request.threadId, STEP_OUT)) {
			dap::StepOutResponse();
		}
		return dap::Error("Could not StepOut");
	}
	dap::ResponseOrError<dap::NextResponse> PapyrusDebugger::Next(const dap::NextRequest& request)
	{
		if (m_executionManager->Step(request.threadId, STEP_OVER)) {
			dap::NextResponse();
		}
		return dap::Error("Could not Next");
	}
	dap::ResponseOrError<dap::ScopesResponse> PapyrusDebugger::GetScopes(const dap::ScopesRequest& request)
	{
		dap::ScopesResponse response;
		const auto vm = RE::BSScript::Internal::VirtualMachine::GetSingleton();
		RE::BSSpinLockGuard lock(vm->runningStacksLock);

		std::vector<std::shared_ptr<StateNodeBase>> frameScopes;
		if (!m_runtimeState->ResolveChildrenByParentId(request.frameId, frameScopes)) {
			return dap::Error("No scopes for frameId %d", request.frameId);
		}

		for (const auto& frameScope : frameScopes)
		{
			auto asScopeSerializable = dynamic_cast<IProtocolScopeSerializable*>(frameScope.get());
			if (!asScopeSerializable)
			{
				continue;
			}

			dap::Scope scope;
			if (!asScopeSerializable->SerializeToProtocol(scope))
			{
				continue;
			}
			
			response.scopes.push_back(scope);
		}

		return response;
	}
	dap::ResponseOrError<dap::VariablesResponse> PapyrusDebugger::GetVariables(const dap::VariablesRequest& request)
	{
		dap::VariablesResponse response;

		const auto vm = RE::BSScript::Internal::VirtualMachine::GetSingleton();
		RE::BSSpinLockGuard lock(vm->runningStacksLock);

		std::vector<std::shared_ptr<StateNodeBase>> variableNodes;
		if (!m_runtimeState->ResolveChildrenByParentId(request.variablesReference, variableNodes)) {
			return dap::Error("No such variable reference %d", request.variablesReference);
		}

		// TODO: support `start`, `filter`, parameter
		int count = 0;
		int maxCount = request.count.value(variableNodes.size());
		for (const auto& variableNode : variableNodes)
		{
			if (count > maxCount) {
				break;
			}
			auto asVariableSerializable = dynamic_cast<IProtocolVariableSerializable*>(variableNode.get());
			if (!asVariableSerializable)
			{
				continue;
			}

			dap::Variable variable;
			if (!asVariableSerializable->SerializeToProtocol(variable))
			{
				continue;
			}
			
			response.variables.push_back(variable);
			count++;
		}

		return response;
	}
	dap::ResponseOrError<dap::SourceResponse> PapyrusDebugger::GetSource(const dap::SourceRequest& request)
	{
		if (!request.source.has_value() || !request.source.value().name.has_value()) {
			if (!request.sourceReference) {
				return dap::Error("No source name or sourceReference");
			} else {
				// TODO: Support this?
				return dap::Error("No source name");
			}
		}
		std::string name = request.source.value().name.value();
		dap::SourceResponse response;
		if (m_pexCache->GetDecompiledSource(name.c_str(), response.content)) {
			return response;
		}
		return dap::Error("Could not find source " + name);
	}
	dap::ResponseOrError<dap::LoadedSourcesResponse> PapyrusDebugger::GetLoadedSources(const dap::LoadedSourcesRequest& request)
	{
		dap::LoadedSourcesResponse response;
		const auto vm = RE::BSScript::Internal::VirtualMachine::GetSingleton();
		RE::BSSpinLockGuard lock(vm->typeInfoLock);

		for (const auto& script : vm->objectTypeMap)
		{
			dap::Source source;
			std::string scriptName = script.first.c_str();
			if (m_pexCache->GetSourceData(scriptName.c_str(), source))
			{
				response.sources.push_back(source);
			}
		}
		return response;
	}
}
