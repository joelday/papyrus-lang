#pragma once


#include "GameInterfaces.h"

#include "BreakpointManager.h"
#include "RuntimeState.h"
#include <dap/session.h>

#include <mutex>

namespace DarkId::Papyrus::DebugServer
{
	enum StepType {
		STEP_IN = 0,
		STEP_OVER,
		STEP_OUT
	};

	using namespace RE::BSScript::Internal;

	class DebugExecutionManager
	{
		enum class DebuggerState
		{
			kRunning = 0,
			kPaused,
			kStepping
		};

		std::mutex m_instructionMutex;
		bool m_closed = false;

		std::shared_ptr<dap::Session> m_session;
		RuntimeState* m_runtimeState;
		BreakpointManager* m_breakpointManager;

		DebuggerState m_state = DebuggerState::kRunning;
		uint32_t m_currentStepStackId = 0;
		StepType m_currentStepType = StepType::STEP_IN;
		RE::BSScript::StackFrame* m_currentStepStackFrame;
	public:
		explicit DebugExecutionManager(std::shared_ptr<dap::Session> session, RuntimeState* runtimeState,
									   BreakpointManager* breakpointManager)
			: m_session(session), m_runtimeState(runtimeState), m_breakpointManager(breakpointManager),
			  m_currentStepStackFrame(nullptr)
		{
		}

		void Close();
		void HandleInstruction(CodeTasklet* tasklet, CodeTasklet::OpCode opCode);
		bool Continue();
		bool Pause();
		bool Step(uint32_t stackId, StepType stepType);
	};
}
