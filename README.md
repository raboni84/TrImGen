# TrImGen - Triage Image Generator

A small tool to generate a triage image of a running system, a raw image or a virtual disk. With the help of DiscUtils a large number of partition and virtual disk types are automatically detected and traversable. Although dotnet core is capable of cross platform execution, sadly only Windows is currently supported for live triage image generation. Offline disk images shouldn't be a problem though.

TrImGen is highly configurable in regards of the resulting image type, formatting and what files to look for. In addition to that it provides automatic parsing and value extraction of windows registry and event log files.

The development of this program is kind of a stop and go thing, so don't expect regular updates with new features or bugfixes. In my free time I have other more important things to do :D
