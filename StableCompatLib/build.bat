g++ -c StableCompatLib.cpp
g++ -shared -static -static-libgcc -static-libstdc++ -lm -o StableCompatLib.dll StableCompatLib.o
