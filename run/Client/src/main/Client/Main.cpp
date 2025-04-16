#include <Core/Core.hpp>
#include <iostream>
#include <string>

int main(int argc, char** argv) {
	std::cout << "Hello World!" << std::endl;
	Core::init();

	std::string str;
	std::getline(std::cin, str);
	std::cout << "Input: " << str << std::endl;

	return 0;
}
