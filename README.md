# Elektron MachineDrum SysEx dump -> MIDI

This is a simple tool to convert pattern data from MachineDrum SysEx dump files into MIDI format. When Autechre released SysEx dumps for their 2008 live set, which included SysEx data from both the MachineDrum and MonoMachine, I wanted to have a look at the patterns. However, not owning either of these machines, the only option was to convert them into a more common format. If there is enough demand, I might expand the current implementation to handle things like accents and kit contents. 

This tool could also be useful as a starting point for anyone wishing to utilize patterns or other data from their MachineDrum/MonoMachine devices.

![One of the resulting MIDI files imported into Ableton Live](https://raw.github.com/carrierdown/elektron-sysex-to-midi/master/pattern.png)

## What's contained in this repository?

- Source code for the tool itself
- Windows (x64) executable in the `dist` folder (more platforms can be added if there is demand)
- The extracted MIDI files from the Autechre SysEx dumps (`ae-machinedrum-midi.zip`)

## Possible enhancements

- Support for MonoMachine
- Support for additional pattern data like accents & swing
- Conversion of the kit information into a human-readable format.

## Contact

If you have any questions or suggestions, hit me up at <http://twitter.com/knupland>. Alternatively, you can file an issue in this repo if it's a more general request regarding this tool.
