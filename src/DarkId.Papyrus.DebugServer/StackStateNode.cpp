#include "StackStateNode.h"

#include "RuntimeState.h"
#include "Utilities.h"

#include <string>
#include "StackFrameStateNode.h"

namespace DarkId::Papyrus::DebugServer
{
	StackStateNode::StackStateNode(const uint32_t stackId) : m_stackId(stackId)
	{
	}

	bool StackStateNode::SerializeToProtocol(Thread& thread) const
	{
		thread.id = m_stackId;

		std::vector<RE::BSScript::StackFrame*> frames;
		RuntimeState::GetStackFrames(m_stackId, frames);

		if (frames.empty())
		{
			thread.name = StringFormat("(%d)", thread.id);
		}
		else
		{
			const auto frame = frames.back();
			const auto name = frame->owningObjectType ? frame->owningObjectType->GetName() : "<unknown>";
			thread.name = StringFormat("%s (%d)", name, thread.id);
		}

		// TODO: This isn't even in the DAP spec.
		thread.running = true; // m_state != DebuggerState::kState_Paused;

		return true;
	}

	bool StackStateNode::GetChildNames(std::vector<std::string>& names)
	{
		std::vector<RE::BSScript::StackFrame*> frames;
		RuntimeState::GetStackFrames(m_stackId, frames);

		for (auto i = 0; i < frames.size(); i++)
		{
			names.push_back(std::to_string(i));
		}

		return true;
	}

	bool StackStateNode::GetChildNode(const std::string name, std::shared_ptr<StateNodeBase>& node)
	{
		int level;
		if (!ParseInt(name, &level))
		{
			return false;
		}

		std::vector<RE::BSScript::StackFrame*> frames;
		if (!RuntimeState::GetStackFrames(m_stackId, frames))
		{
			return false;
		}

		if (level >= frames.size())
		{
			return false;
		}

		node = std::make_shared<StackFrameStateNode>(frames.at(level));

		return true;
	}
}
