// In JSON map keys can only be strings, so here's a class which makes conversion to/from string easy
#pragma once

#include <string>

template <typename T>
std::string castToString(const T& value);

// template specializations

std::string castToString(const bool& value);
std::string castToString(const int& value);
std::string castToString(const float& value);
std::string castToString(const std::string& value);

template <typename T>
T fromString(const std::string& value);

template <>
bool fromString(const std::string& valueStr);

template <>
int fromString(const std::string& valueStr);

template <>
float fromString(const std::string& valueStr);

template <>
std::string fromString(const std::string& valueStr);


// return empty string if no conversion possible
template <typename T>
std::string castToString(const T& /* value */)
{
    return std::string();
}

template <typename T>
T fromString(const std::string& /* value */)
{
    return T();
}
