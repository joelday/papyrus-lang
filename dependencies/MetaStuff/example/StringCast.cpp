#include "StringCast.h"

std::string castToString(const bool& value)
{
    return value ? "true" : "false";
}

std::string castToString(const int& value)
{
    return std::to_string(value);
}

std::string castToString(const float& value)
{
    return std::to_string(value);
}

std::string castToString(const std::string& value)
{
    return value;
}

// from string

template <>
bool fromString(const std::string& valueStr)
{
    if (valueStr == "true") {
        return true;
    } else if (valueStr == "false") {
        return false;
    }
}

template <>
int fromString(const std::string& valueStr)
{
    return std::stoi(valueStr);
}

template <>
float fromString(const std::string& valueStr)
{
    return std::stof(valueStr);
}

template <>
std::string fromString(const std::string& valueStr)
{
    return valueStr;
}
