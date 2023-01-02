#pragma once

#include "Meta.h"
#include "FormTypeMacros.h"
#include "GameInterfaces.h"

#ifndef STRING
#define STRING(s) #s
#endif

/**
 *		Notes:
 * 1.	Using a library called MetaStuff in order to statically declare reflection
 *		metadata for form and form component types. This is tedious and has a
 *		decent maintenance burden, but there isn't really any way to infer it all.
 *		
 * 2.	Given that maintenance burden, it probably makes sense to do this incrementally,
 *		prioritizing useful types like GlobalVariable along with existing native
 *		getters.
 *		
 * 3.	Made changes to MetaStuff to allow for a lambda expression to return a value.
 * 
 * 4.	The way I managed that involved loss of const correctness.
 * 
 * 5.	Very little is being passed around by ref now, but most non-trivial values
 *		aren't copy constructable anyway and are being passed as pointers.
 *		
 * 6.	Didn't figure out how to get some template argument deduction to work,
 *		so a lot of the macros exist to reduce explicit argument verbosity.
 *		
 * 7.	I'm not too concerned with concurrency because this is all being executed
 *		while paused in the debugger.
 *		
 * 8.	I'm sure these macros are all using ##?## way more than is necessary.
 */

namespace meta
{
#define VIRT_FUNC_MEMBER_IMPL(type, returnType, funcName, name) \
	member<##type##, ##returnType##>(STRING(name), [](##type##* value) { return value->##funcName##(); }) \

#define VIRT_FUNC_MEMBER(type, returnType, name) \
	VIRT_FUNC_MEMBER_IMPL(type, returnType, name, name) \

#define VIRT_FUNC_MEMBER_IMPL_ARGS(type, returnType, funcName, name, ...) \
	member<##type##, ##returnType##>(STRING(name), [](##type##* value) { return value->##funcName##(__VA_ARGS__); }) \

#define VIRT_FUNC_MEMBER_ARGS(type, returnType, name, ...) \
	VIRT_FUNC_MEMBER_IMPL_ARGS(type, returnType, name, name, __VA_ARGS__) \
	
#define VIRT_FUNC_GET_MEMBER(type, returnType, name) \
	VIRT_FUNC_MEMBER_IMPL(type, returnType, Get##name##, name) \
	
#define BASE_TYPE_MEMBER(type, baseType) \
	member<##type##, ##baseType##*>(typeid(##baseType##).name(), [](##type##* value) { return value; }) \

	template <typename Class>
	std::string toDisplayValue(Class value)
	{
		return std::string();
	}

	template <>
	inline std::string toDisplayValue<RE::TESForm*>(RE::TESForm* value)
	{
		char description[512];
		value->GetFormDetailedString(description, sizeof(description));		
		return std::string(description);
	}

	template <>
	inline auto registerMembers<RE::TESForm>()
	{
#if SKYRIM
		using VMTypeID = RE::VMTypeID;
#else // FALLOUT
		using VMTypeID = std::uint32_t;
#endif
		return members(
			member<RE::TESForm, RE::BSScript::Object*>("VM Object", [](RE::TESForm* form)
				{
					auto vm = RE::BSScript::Internal::VirtualMachine::GetSingleton();

					RE::BSTSmartPointer<RE::BSScript::ObjectTypeInfo> classType;

					if (vm->GetScriptObjectType(static_cast<VMTypeID>(form->GetFormType()), classType))
					{
#if SKYRIM
						const auto handle = vm->GetObjectHandlePolicy2()->GetHandleForObject(static_cast<VMTypeID>(form->GetFormType()), form);
						if (vm->GetObjectHandlePolicy2()->IsHandleObjectAvailable(handle))
						{
							RE::BSTSmartPointer<RE::BSScript::Object> object;
							if (vm->FindBoundObject(handle, classType->GetName(), object))
							{
								return object.get();
							}
						}
#else // FALLOUT
						const auto handle = vm->GetObjectHandlePolicy().GetHandleForObject(static_cast<VMTypeID>(form->GetFormType()), form);
						if (vm->GetObjectHandlePolicy().IsHandleObjectAvailable(handle))
						{
							RE::BSTSmartPointer<RE::BSScript::Object> object;
							if (vm->FindBoundObject(handle, classType->GetName(), true, object, false))
							{
								return object.get();
							}
						}
#endif
					}

					return static_cast<RE::BSScript::Object*>(nullptr);
				}),

			VIRT_FUNC_MEMBER(RE::TESForm, bool, GetKnown),
#ifdef SKYRIM
			VIRT_FUNC_MEMBER(RE::TESForm, bool, GetPlayable),
#else
			VIRT_FUNC_MEMBER_ARGS(RE::TESForm, bool, GetPlayable, nullptr),
#endif
			VIRT_FUNC_MEMBER(RE::TESForm, bool, IsHeadingMarker),
			VIRT_FUNC_MEMBER(RE::TESForm, bool, GetIgnoredBySandbox),
			VIRT_FUNC_MEMBER(RE::TESForm, bool, IsBoundObject),
			VIRT_FUNC_MEMBER(RE::TESForm, bool, IsMagicItem)
		);
	}
	
	template <>
	inline std::string toDisplayValue<RE::BGSKeyword*>(RE::BGSKeyword* value)
	{
		// TODO: Get rid of this manual quoting concatenation crap
		return "\"" + std::string(value->formEditorID.c_str()) + "\"";
	}
	
	template <>
	inline auto registerMembers<RE::BGSKeyword>()
	{
		return members(
			BASE_TYPE_MEMBER(RE::BGSKeyword, RE::TESForm),
			member("Keyword", &RE::BGSKeyword::formEditorID)
		);
	}

	template <>
	inline auto registerMembers<RE::BGSLocationRefType>()
	{
		return members(
			BASE_TYPE_MEMBER(RE::BGSLocationRefType, RE::BGSKeyword)
		);
	}

	template <>
	inline auto registerMembers<RE::BGSAction>()
	{
		return members(
			BASE_TYPE_MEMBER(RE::BGSAction, RE::BGSKeyword),
			member("Index", &RE::BGSAction::index)
		);
	}
	
	template <>
	inline auto registerMembers<RE::TESObject>()
	{
		return members(
			BASE_TYPE_MEMBER(RE::TESObject, RE::TESForm),
			VIRT_FUNC_MEMBER(RE::TESObject, bool, IsBoundAnimObject),
			VIRT_FUNC_GET_MEMBER(RE::TESObject, RE::TESWaterForm*, WaterType),
			VIRT_FUNC_MEMBER(RE::TESObject, bool, IsAutoCalc),
			VIRT_FUNC_MEMBER(RE::TESObject, bool, IsMarker),
			VIRT_FUNC_MEMBER(RE::TESObject, bool, IsOcclusionMarker)
		);
	}

	template <>
	inline auto registerMembers<RE::TESBoundObject::BOUND_DATA>()
	{
		return members(
			member("boundMin", &RE::TESBoundObject::BOUND_DATA::boundMin),
			member("boundMax", &RE::TESBoundObject::BOUND_DATA::boundMax)
		);
	}
	
	template <>
	inline auto registerMembers<RE::TESBoundObject>()
	{
		return members(
			BASE_TYPE_MEMBER(RE::TESBoundObject, RE::TESObject),
			member("BoundData", &RE::TESBoundObject::boundData)
		);
	}

	//template <>
	//inline auto registerMembers<RE::NiRefObject>()
	//{
	//	return members();
	//}
	
	//template <>
	//inline auto registerMembers<RE::NiObject>()
	//{
	//	return members(
	//		BASE_TYPE_MEMBER(RE::NiObject, RE::NiRefObject)
	//	);
	//}
	//
	//template <>
	//inline auto registerMembers<RE::BSTextureSet>()
	//{
	//	return members(
	//		BASE_TYPE_MEMBER(RE::BSTextureSet, RE::NiObject)
	//	);
	//}

	//template <>
	//inline auto registerMembers<RE::TESTexture>()
	//{
	//	return members(
	//		VIRT_FUNC_GET_MEMBER(RE::TESTexture, uint32_t, Size),
	//		VIRT_FUNC_GET_MEMBER(RE::TESTexture, std::string, SearchDir),
	//		member("Texture", &RE::TESTexture::texture)
	//	);
	//}
	//
	//template <>
	//inline auto registerMembers<RE::BGSTextureSet>()
	//{
	//	return members(
	//		BASE_TYPE_MEMBER(RE::BGSTextureSet, RE::TESBoundObject),
	//		BASE_TYPE_MEMBER(RE::BGSTextureSet, RE::BSTextureSet),
	//		
	//		// TODO: Simplified pattern/macro for this
	//		member<RE::BGSTextureSet, std::vector<RE::TESTexture*>>("Textures", [](RE::BGSTextureSet* form)
	//			{
	//				// TODO: Is this a correct way to do this?
	//				const uint32_t elementCount = sizeof(form->textures) / sizeof(RE::TESTexture);
	//			
	//				std::vector<RE::TESTexture*> elements;
	//				
	//				for (auto i = 0; i < elementCount; i++)
	//				{
	//					elements.push_back(&form->textures[i]);
	//				}
	//		
	//				return elements;

	//				// TODO: Tried this, but need each element to be a pointer.
	//				// return std::vector<RE::TESTexture*>(form->textures, std::end(form->textures));
	//			})
	//	);
	//}

	//template <>
	//inline auto registerMembers<RE::TESIcon>()
	//{
	//	return members(
	//		BASE_TYPE_MEMBER(RE::TESIcon, RE::TESTexture)
	//	);
	//}
	//
	//template <>
	//inline auto registerMembers<RE::BGSMenuIcon>()
	//{
	//	return members(
	//		BASE_TYPE_MEMBER(RE::BGSMenuIcon, RE::TESForm),
	//		BASE_TYPE_MEMBER(RE::BGSMenuIcon, RE::TESIcon)
	//	);
	//}
	template <>
	inline std::string toDisplayValue<RE::TESGlobal*>(RE::TESGlobal* value)
	{
		return DarkId::Papyrus::DebugServer::StringFormat("%f", value->value);
	}
	
	template <>
	inline auto registerMembers<RE::TESGlobal>()
	{
		return members(
			BASE_TYPE_MEMBER(RE::TESGlobal, RE::TESForm),
			member("Value", &RE::TESGlobal::value)
		);
	}
	
	template <>
	inline auto registerMembers<RE::TESFullName>()
	{
		return members(
			member("FullName", &RE::TESFullName::fullName)
		);
	}
	
	template <>
	inline auto registerMembers<RE::TESDescription>()
	{
		return members(
			// TODO
		);
	}


	//// TODO: Skill enum
	//
	//template <>
	//inline auto registerMembers<RE::TESClass::Data::SkillWeights>()
	//{
	//	return members(
	//		// TODO
	//	);
	//}
	
	//template <>
	//inline auto registerMembers<RE::TESClass::Data::AttributeWeights>()
	//{
	//	return members(
	//		member("Health", &RE::TESClass::Data::AttributeWeights::health),
	//		member("Magicka", &RE::TESClass::Data::AttributeWeights::magicka),
	//		member("Stamina", &RE::TESClass::Data::AttributeWeights::stamina)
	//	);
	//}
	//
	//template <>
	//inline auto registerMembers<RE::TESClass::Data>()
	//{
	//	return members(
	//		member("Teaches", &RE::TESClass::Data::teaches),
	//		member("MaximumTrainingLevel", &RE::TESClass::Data::maximumTrainingLevel),
	//		member("SkillWeights", &RE::TESClass::Data::skillWeights),
	//		member("BleedoutDefault", &RE::TESClass::Data::bleedoutDefault),
	//		member("VoicePoints", &RE::TESClass::Data::voicePoints),
	//		member("AttributeWeights", &RE::TESClass::Data::attributeWeights)
	//	);
	//}
	//
	//template <>
	//inline auto registerMembers<RE::TESClass>()
	//{
	//	return members(
	//		BASE_TYPE_MEMBER(RE::TESClass, RE::TESForm),
	//		BASE_TYPE_MEMBER(RE::TESClass, RE::TESFullName),
	//		BASE_TYPE_MEMBER(RE::TESClass, RE::TESDescription),
	//		BASE_TYPE_MEMBER(RE::TESClass, RE::TESTexture),
	//		member("Data", &RE::TESClass::data)
	//	);
	//}

	//template <>
	//inline auto registerMembers<RE::TESReactionForm>()
	//{
	//	return members(
	//		// TODO
	//	);
	//}
	//
	template <>
	inline auto registerMembers<RE::TESFaction>()
	{
		return members(
			BASE_TYPE_MEMBER(RE::TESFaction, RE::TESForm),
			BASE_TYPE_MEMBER(RE::TESFaction, RE::TESFullName),
			BASE_TYPE_MEMBER(RE::TESFaction, RE::TESReactionForm)
		);
	}
}
