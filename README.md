# Shed - .NET runtine inspector

Shed is an application that allow to inspect the .NET runtime of a program in order to extract useful information. 
It can be used to inspect malicious applications in order to have a first general 
overview of which information are stored once that the malware is executed.

Shed is able to:
* Extract all objects stored in the managed heap
* Print strings stored in memory
* Save the snapshot of the heap in a JSON format for post-processing
* Dump all modules that are loaded in memory

# Using Shed

## Inspecting an already running application
In order to inspect an already running process you have to pass the pid to shed. Example:

c:\Shed.exe --pid 2356

## Inspecting a binary
In order to inspect a binary, Shed needs to execute it and to attach to it in order to inspect the runtime. Example:

c:\Shed.exe --exe <path to file>.exe

You can also specify the amount of time (in milliseconds) to wait before to suspend the process. This will allow the program to have the time to initialize its properties. Example:

c:\Shed.exe --timeout 2000 --exe <path to file>.exe

## Dumping options
By default Shed dump both the heap and the modules. If you want only one of that specify the --dump-heap option to dump only the objects in the heap or the --dump-modules to dump only the modules.

## License information

Copyright (C) 2017 Antonio Parata - @s4tan

License: GNU General Public License, version 2 or later; see LICENSE included in this archive for details.