#pragma once

#include "IdMap.h"

#include "GameInterfaces.h"
#include <memory>
#include <vector>
#include "StateNodeBase.h"

namespace DarkId::Papyrus::DebugServer
{
	class RuntimeState
	{
		std::unique_ptr<IdMap<std::string>> m_paths;

	public:
		explicit RuntimeState(const std::shared_ptr<IdProvider>& idProvider);

		bool ResolveStateByPath(std::string requestedPath, std::shared_ptr<StateNodeBase>& node);
		bool ResolveStateById(uint32_t id, std::shared_ptr<StateNodeBase>& node);
		bool ResolveChildrenByParentPath(std::string requestedPath, std::vector<std::shared_ptr<StateNodeBase>>& nodes);
		bool ResolveChildrenByParentId(uint32_t id, std::vector<std::shared_ptr<StateNodeBase>>& nodes);

		static std::shared_ptr<StateNodeBase> CreateNodeForVariable(std::string name, const RE::BSScript::Variable* variable);
		
		static RE::BSTSmartPointer<RE::BSScript::Stack> GetStack(uint32_t stackId);
		static RE::BSScript::StackFrame* GetFrame(uint32_t stackId, uint32_t level);

		static void GetStackFrames(RE::BSTSmartPointer<RE::BSScript::Stack> stack, std::vector<RE::BSScript::StackFrame*>& frames);
		static bool GetStackFrames(uint32_t stackId, std::vector<RE::BSScript::StackFrame*>& frames);
	};
}

