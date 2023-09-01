#pragma once
#include <functional>

#include <eventpp/callbacklist.h>

#include "GameInterfaces.h"

#define EVENT_DECLARATION(NAME, HANDLER_SIGNATURE) \
	typedef eventpp::CallbackList<HANDLER_SIGNATURE>::Handle NAME##EventHandle; \
	NAME##EventHandle SubscribeTo##NAME(std::function<HANDLER_SIGNATURE> handler); \
	bool UnsubscribeFrom##NAME(NAME##EventHandle handle);

namespace DarkId::Papyrus::DebugServer
{
	namespace RuntimeEvents
	{
		EVENT_DECLARATION(InstructionExecution, void(RE::BSScript::Internal::CodeTasklet*, uint32_t actualIP))
		EVENT_DECLARATION(CreateStack, void(RE::BSTSmartPointer<RE::BSScript::Stack>&))
		EVENT_DECLARATION(CleanupStack, void(uint32_t))
		// EVENT_DECLARATION(InitScript, void(RE::TESInitScriptEvent*))
		EVENT_DECLARATION(Log, void(const RE::BSScript::LogEvent*))

		namespace Internal
		{
			void CommitHooks();
		}
	}
}

#undef EVENT_DECLARATION