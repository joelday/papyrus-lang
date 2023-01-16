#include "RuntimeState.h"
#include "Utilities.h"
#include "StateNodeBase.h"

#include "StackStateNode.h"
#include "ObjectStateNode.h"

#include "ArrayStateNode.h"
#include "ValueStateNode.h"

#if FALLOUT
#include "StructStateNode.h"
#include "RE/Bethesda/BSScriptUtil.h"
namespace RE {
	using BSSpinLockGuard = BSAutoLock<BSSpinLock, BSAutoLockDefaultPolicy>;
}
#endif

namespace DarkId::Papyrus::DebugServer
{
	using namespace RE::BSScript::Internal;

	RuntimeState::RuntimeState(const std::shared_ptr<IdProvider>& idProvider)
	{
		m_paths = std::make_unique<IdMap<std::string>>(idProvider);
	}

	bool RuntimeState::ResolveStateByPath(const std::string requestedPath, std::shared_ptr<StateNodeBase>& node)
	{
		const auto path = ToLowerCopy(requestedPath);

		auto elements = Split(path, ".");

		if (elements.empty())
		{
			return false;
		}

		const auto stackIdElement = elements.at(0);
		int stackId;
		if (!ParseInt(stackIdElement, &stackId))
		{
			return false;
		}

		std::vector<std::string> currentPathElements;
		currentPathElements.push_back(stackIdElement);

		elements.erase(elements.begin());

		std::shared_ptr<StateNodeBase> currentNode = std::make_shared<StackStateNode>(stackId);

		while (!elements.empty() && currentNode)
		{
			auto structured = dynamic_cast<IStructuredState*>(currentNode.get());
			
			if (structured)
			{
				std::vector<std::string> childNames;
				structured->GetChildNames(childNames);

				for (auto childName : childNames)
				{
					ToLower(childName);
					auto discoveredElements(currentPathElements);
					discoveredElements.push_back(childName);

					const auto discoveredPath = Join(discoveredElements, ".");

					uint32_t addedId;
					m_paths->AddOrGetExisting(discoveredPath, addedId);
				}
			}
			
			const auto currentName = elements.at(0);
			currentPathElements.push_back(currentName);
		
			if (structured && !structured->GetChildNode(currentName, currentNode))
			{
				currentNode = nullptr;
				break;
			}
		
			elements.erase(elements.begin());
		}

		if (!currentNode)
		{
			return false;
		}

		node = currentNode;

		if (currentPathElements.size() > 1)
		{
			uint32_t id;
			m_paths->AddOrGetExisting(path, id);
			node->SetId(id);
		}
		else
		{
			node->SetId(stackId);
		}

		return true;
	}

	bool RuntimeState::ResolveStateById(const uint32_t id, std::shared_ptr<StateNodeBase>& node)
	{
		std::string path;

		if (m_paths->Get(id, path))
		{
			return ResolveStateByPath(path, node);
		}

		return false;
	}

	bool RuntimeState::ResolveChildrenByParentPath(const std::string requestedPath, std::vector<std::shared_ptr<StateNodeBase>>& nodes)
	{
		std::shared_ptr<StateNodeBase> resolvedParent;
		if (!ResolveStateByPath(requestedPath, resolvedParent))
		{
			return false;
		}

		auto structured = dynamic_cast<IStructuredState*>(resolvedParent.get());
		if (!structured)
		{
			return false;
		}

		std::vector<std::string> childNames;
		structured->GetChildNames(childNames);

		for (const auto& childName : childNames)
		{
			std::shared_ptr<StateNodeBase> childNode;
			if (ResolveStateByPath(StringFormat("%s.%s", requestedPath.c_str(), childName.c_str()), childNode))
			{
				nodes.push_back(childNode);
			}
		}

		return true;
	}

	bool RuntimeState::ResolveChildrenByParentId(const uint32_t id, std::vector<std::shared_ptr<StateNodeBase>>& nodes)
	{
		std::string path;

		if (m_paths->Get(id, path))
		{
			return ResolveChildrenByParentPath(path, nodes);
		}

		return false;
	}

	std::shared_ptr<StateNodeBase> RuntimeState::CreateNodeForVariable(std::string name, const RE::BSScript::Variable* variable)
	{
#if SKYRIM
		if (variable->IsObject())
		{
			auto obj = variable->GetObject();
			auto typeinfo = obj ? obj->GetTypeInfo() : variable->GetType().GetTypeInfo();

			return std::make_shared<ObjectStateNode>(name, obj ? obj.get() : nullptr, typeinfo);
		}
		if (variable->IsArray())
		{
			auto arr = variable->GetArray();
			if (arr){
				auto &typeinfo = arr->type_info();
				return std::make_shared<ArrayStateNode>(name, arr ? arr.get() : nullptr, &typeinfo);
			} else {
				return std::make_shared<ArrayStateNode>(name, arr ? arr.get() : nullptr, variable->GetType());
			}

		}
		if (variable->IsBool() || variable->IsFloat() || variable->IsInt() || variable->IsString())
		{
			return std::make_shared<ValueStateNode>(name, variable);
		}
#else // FALLOUT
		if (variable->is<RE::BSScript::Object>())
		{	
			auto obj = RE::BSScript::get<RE::BSScript::Object>(*variable);
			auto typeinfo = obj ? obj->GetTypeInfo() : variable->GetType().GetObjectTypeInfo();
  
			return std::make_shared<ObjectStateNode>(name, obj ? obj.get() : nullptr, typeinfo);
		}
		if (variable->is<RE::BSScript::Array>())
		{
			auto arr = RE::BSScript::get<RE::BSScript::Array>( *variable);
			if (arr){
				//auto &typeinfo = arr->type_info();
				return std::make_shared<ArrayStateNode>(name, arr ? arr.get() : nullptr, &arr->type_info());
			} else {
				auto type = variable->GetType();
				return std::make_shared<ArrayStateNode>(name, arr ? arr.get() : nullptr, type);
			}
		}
		if (variable->is<bool>() || variable->is<float>() || variable->is<std::int32_t>() || variable->is<std::uint32_t>() || variable->is<RE::BSFixedString>())
		{
			return std::make_shared<ValueStateNode>(name, variable);
		}
		if (variable->is<RE::BSScript::Variable>())
		{
			auto var = RE::BSScript::get<RE::BSScript::Variable>(*variable);
			return CreateNodeForVariable(name, var);
		}
		
		if (variable->is<RE::BSScript::Struct>())
		{
			auto _struct = RE::BSScript::get<RE::BSScript::Struct>(*variable);
			auto typeinfo = _struct ? _struct->type.get() : variable->GetType().GetStructTypeInfo();

			return std::make_shared<StructStateNode>(name, _struct.get(), typeinfo);
		}
#endif
		return nullptr;
	}

	RE::BSTSmartPointer<RE::BSScript::Stack> RuntimeState::GetStack(uint32_t stackId)
	{
		const auto vm = VirtualMachine::GetSingleton();
		RE::BSSpinLockGuard lock(vm->runningStacksLock);

		const auto tableItem = vm->allRunningStacks.find(stackId);
		if (tableItem == vm->allRunningStacks.end())
		{
			return nullptr;
		}

		return tableItem->second;
	}

	RE::BSScript::StackFrame* RuntimeState::GetFrame(const uint32_t stackId, const uint32_t level)
	{
		std::vector<RE::BSScript::StackFrame*> frames;
		GetStackFrames(stackId, frames);

		if (frames.empty() || level > frames.size() - 1)
		{
			return nullptr;
		}

		return frames.at(level);
	}

	void RuntimeState::GetStackFrames(const RE::BSTSmartPointer<RE::BSScript::Stack> stack, std::vector<RE::BSScript::StackFrame*>& frames)
	{
		const auto vm = VirtualMachine::GetSingleton();
		RE::BSSpinLockGuard lock(vm->runningStacksLock);

		auto frame = stack->top;

		while (frame)
		{
			frames.push_back(frame);
			frame = frame->previousFrame;
		}
	}

	bool RuntimeState::GetStackFrames(const uint32_t stackId, std::vector<RE::BSScript::StackFrame*>& frames)
	{
		const auto vm = VirtualMachine::GetSingleton();
		RE::BSSpinLockGuard lock(vm->runningStacksLock);

		const auto stack = GetStack(stackId);
		if (!stack)
		{
			return false;
		}

		GetStackFrames(stack, frames);

		return true;
	}
}
