# Elektron MachineDrum SysEx dump -> MIDI

This is a simple tool to convert pattern data from MachineDrum SysEx dump files into MIDI format. When Autechre released SysEx dumps for their 2008 live set, which included SysEx data from both the MachineDrum and MonoMachine, I wanted to have a look at the patterns. However, not owning either of these machines, the only option was to convert them into a more common format. The current implementation takes care of the bare-bones MIDI conversion, but could be expanded to handle things like accents and kit contents to help map the MIDI notes correctly. 

This tool could also be useful as a starting point for anyone wishing to utilize patterns or other data from their MachineDrum/MonoMachine devices.

## Features to be added

- Support for full 64-step patterns (for now only mk1 data is parsed, resulting in 32-step patterns)
- Support for accents
- Possibly a conversion of the kit information into a human-readable format.
