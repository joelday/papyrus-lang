#include "DebugExecutionManager.h"
#include <thread>
#include "Window.h"

namespace DarkId::Papyrus::DebugServer
{
	using namespace RE::BSScript::Internal;

	void DebugExecutionManager::HandleInstruction(CodeTasklet* tasklet)
	{
		std::lock_guard<std::mutex> lock(m_instructionMutex);

		if (m_closed)
		{
			return;
		}
		
		const auto & func = tasklet->topFrame->owningFunction;
		bool shouldContinue = false;
		bool shouldSendEvent = false;
		std::string pauseReason = "";
		DebuggerState new_state = m_state;

		if (m_state == DebuggerState::kPaused)
		{
			pauseReason = "paused";
		}
		else if (m_state != DebuggerState::kPaused && m_breakpointManager->GetExecutionIsAtValidBreakpoint(tasklet))
		{
			pauseReason = "breakpoint";
		}
		else if (m_state == DebuggerState::kStepping && !RuntimeState::GetStack(m_currentStepStackId))
		{
			shouldContinue = true;
		}
		else if (m_state == DebuggerState::kStepping && tasklet->stack->stackID == m_currentStepStackId)
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
						pauseReason = "step";
						break;
					case StepType::STEP_OUT:
						// If the stack exists, but the original frame is gone, we know we're in a previous frame now.
						if (stepFrameIndex == -1)
						{
							pauseReason = "step";
						}
						break;
					case StepType::STEP_OVER:
						if (stepFrameIndex <= 0)
						{
							pauseReason = "step";
						}
						break;
					}
				}
			}
		}
		
		if (!pauseReason.empty())
		{	
			m_state = DebuggerState::kPaused;
			m_currentStepStackId = 0;
			m_currentStepStackFrame = nullptr;
			if (m_session) {
				m_session->send(dap::StoppedEvent{
					.reason = pauseReason,
					.threadId = tasklet->stack->stackID
					});
			}
			Window::ReleaseFocus();
		}
		else if (shouldContinue) {
			m_state = DebuggerState::kRunning;
			m_currentStepStackId = 0;
			m_currentStepStackFrame = nullptr;
			if (m_session) {
				m_session->send(dap::ContinuedEvent{
				.allThreadsContinued = true,
				.threadId = tasklet->stack->stackID
					});
			}
		}

		while (m_state == DebuggerState::kPaused && !m_closed)
		{
			using namespace std::chrono_literals;
			std::this_thread::sleep_for(100ms);
		}
		// If we were the thread that paused, regain focus
		if (!pauseReason.empty()) {
			Window::RegainFocus();
		}

	}

	void DebugExecutionManager::Open(std::shared_ptr<dap::Session> ses)
	{
		m_closed = false;
		m_session = ses;
		std::lock_guard<std::mutex> lock(m_instructionMutex);
	}

	void DebugExecutionManager::Close()
	{
		m_closed = true;
		std::lock_guard<std::mutex> lock(m_instructionMutex);
		m_session = nullptr;
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
		if (stackId < 0) {
			return false;
		}

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
