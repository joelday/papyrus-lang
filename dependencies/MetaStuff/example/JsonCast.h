#pragma once

#include <string>
#include <vector>
#include <unordered_map>

#include <json.hpp>

#include <Meta.h>
#include "StringCast.h"

using json = nlohmann::json;

template <typename T>
void to_json(json& j, const T& obj);

template <typename T>
void from_json(const json& j, T& obj);

namespace meta
{

/////////////////// SERIALIZATION

template <typename Class,
    typename = std::enable_if_t <meta::isRegistered<Class>()>>
json serialize(const Class& obj);

template <typename Class,
    typename = std::enable_if_t <!meta::isRegistered<Class>()>,
    typename = void>
json serialize(const Class& obj);

template <typename Class>
json serialize_basic(const Class& obj);

// specialization for std::vector
template <typename T>
json serialize_basic(const std::vector<T>& obj);

// specialization for std::unodered_map
template <typename K, typename V>
json serialize_basic(const std::unordered_map<K, V>& obj);


/////////////////// DESERIALIZATION
//
//template<typename Class>
//Class deserialize(const json& obj);

template <typename Class,
    typename = std::enable_if_t<meta::isRegistered<Class>()>>
void deserialize(Class& obj, const json& object);

template <typename Class,
    typename = std::enable_if_t<!meta::isRegistered<Class>()>,
    typename = void>
void deserialize(Class& obj, const json& object);

// specialization for std::vector
template <typename T>
void deserialize(std::vector<T>& obj, const json& object);

// specialization for std::unodered_map
template <typename K, typename V>
void deserialize(std::unordered_map<K, V>& obj, const json& object);

}

#include "JsonCast.inl"