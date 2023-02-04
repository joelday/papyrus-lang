#pragma once

#include <string>
#include <vector>
#include <unordered_map>

#include "MovieInfo.h"

#include <iostream>

struct Person {
    // add this if you want to register private members:
    // template <>
    // auto meta::registerMembers<Person>();

    void setAge(int a)
    {
        //std::cout << "Age is set by calling setter!\n";
        if (a >= 0 && a < 128) { // sorry, if you're older than 128
            age = a;
        } else {
            std::cout << "Can't set age. " << a << " is out of allowed range\n";
        }
    }

    int getAge() const {
        std::cout << "Got age with getter!\n";
        return age;
    }

    void setName(const std::string& name)
    {
        //std::cout << "Name is set by calling setter!\n";
        this->name = name;
    }

    const std::string& getName() const
    {
        std::cout << "Got name with getter!\n";
        return name;
    }

    int age;
    std::string name;
    float salary;
    std::unordered_map<std::string, std::vector<MovieInfo>> favouriteMovies;
};

#include <Meta.h>

namespace meta {

template <>
inline auto registerMembers<Person>()
{
    return members(
        member("age", &Person::getAge, &Person::setAge), // access through getter/setter only!
        member("name", &Person::getName, &Person::setName), // same, but ref getter/setter
        member("salary", &Person::salary),
        member("favouriteMovies", &Person::favouriteMovies)
    );
}

} // end of namespace meta