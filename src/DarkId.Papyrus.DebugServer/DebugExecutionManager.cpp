#include "DebugExecutionManager.h"
#include <thread>

namespace DarkId::Papyrus::DebugServer
{
	using namespace RE::BSScript::Internal;

	void DebugExecutionManager::HandleInstruction(CodeTasklet* tasklet, CodeTasklet::OpCode opCode)
	{
		std::lock_guard<std::mutex> lock(m_instructionMutex);

		if (m_closed)
		{
			return;
		}
		
		const auto func = tasklet->topFrame->owningFunction;
		auto shouldSendPause = false;
		dap::StoppedEvent event;
		
		if (m_state == DebuggerState::kPaused)
		{
			shouldSendPause = true;
		}
		
		if (m_state != DebuggerState::kPaused && m_breakpointManager->GetExecutionIsAtValidBreakpoint(tasklet))
		{
			m_state = DebuggerState::kPaused;
			event.reason = "breakpoint";
			event.threadId = tasklet->stack->stackID;
			m_session->send(event);
		}

		if (m_state == DebuggerState::kStepping && !RuntimeState::GetStack(m_currentStepStackId))
		{
			m_session->send(dap::ContinuedEvent());
			m_currentStepStackId = 0;
			m_currentStepStackFrame = nullptr;
			
			m_state = DebuggerState::kRunning;
		}
		
		if (m_state == DebuggerState::kStepping && tasklet->stack->stackID == m_currentStepStackId)
		{
			if (m_currentStepStackFrame)
			{
				std::vector<RE::BSScript::StackFrame*> currentFrames;
				RuntimeState::GetStackFrames(tasklet->stack->stackID, currentFrames);

				if (!currentFrames.empty())
				{
					ptrdiff_t stepFrameIndex = -1;
					const auto stepFrameIter = std::find(currentFrames.begin(), currentFrames.end(), m_currentStepStackFrame);

					if (stepFrameIter != currentFrames.end())
					{
						stepFrameIndex = std::distance(currentFrames.begin(), stepFrameIter);
					}

					switch (m_currentStepType)
					{
					case StepType::STEP_IN:
						shouldSendPause = true;
						event.reason = "step";
						break;
					case StepType::STEP_OUT:
						// If the stack exists, but the original frame is gone, we know we're in a previous frame now.
						if (stepFrameIndex == -1)
						{
							shouldSendPause = true;
							event.reason = "step";
						}
						break;
					case StepType::STEP_OVER:
						if (stepFrameIndex <= 0)
						{
							shouldSendPause = true;
							event.reason = "step";
						}
						break;
					}
				}
			}
		}

		if (shouldSendPause)
		{
			m_state = DebuggerState::kPaused;
			m_currentStepStackId = 0;
			m_currentStepStackFrame = nullptr;
			event.threadId = tasklet->stack->stackID;
			m_session->send(event);
		}

		while (m_state == DebuggerState::kPaused && !m_closed)
		{
			using namespace std::chrono_literals;
			std::this_thread::sleep_for(100ms);
		}

	}

	void DebugExecutionManager::Close()
	{
		m_closed = true;
		std::lock_guard<std::mutex> lock(m_instructionMutex);
	}

	bool DebugExecutionManager::Continue()
	{
		m_state = DebuggerState::kRunning;
		m_session->send(dap::ContinuedEvent());

		return true;
	}

	bool DebugExecutionManager::Pause()
	{
		if (m_state == DebuggerState::kPaused)
		{
			return false;
		}

		m_state = DebuggerState::kPaused;
		return true;
	}

	bool DebugExecutionManager::Step(uint32_t stackId, const StepType stepType)
	{
		if (m_state != DebuggerState::kPaused)
		{
			return false;
		}

		const auto stack = RuntimeState::GetStack(stackId);
		if (stack->stackID)
		{
			m_currentStepStackFrame = stack->top;
		}
		else
		{
			return false;
		}

		m_state = DebuggerState::kStepping;
		m_currentStepStackId = stackId;
		m_currentStepType = stepType;

		return true;
	}
}
