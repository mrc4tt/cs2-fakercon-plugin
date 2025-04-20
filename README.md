# cs2-FakeRcon Plugin

A CounterStrikeSharp plugin that provides RCON functionality for CS2 servers.
Like the real RCON but it's not the real RCON.

## Description

fake_rcon is a CS2 server plugin that implements RCON (Remote Console) functionality, allowing server administrators to execute commands remotely. This is a CounterStrikeSharp port of the original Metamod:Source version.

## Features

- Remote server administration through RCON
- Secure password protection
- Configurable command permissions
- Compatible with CS2 servers

## Prerequisites

- CS2 Dedicated Server
- [Metamod:Source](https://www.sourcemm.net/downloads.php?branch=master)
- [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp)

## Installation

1. Download the latest release from the [releases page](https://github.com/mrc4tt/cs2-fakercon-plugin/releases)
2. Extract the contents to your CS2 server's `csgo` directory
3. Configure the plugin using the config file at `configs/plugins/fakercon/fakercon.json`

## Configuration

Example configuration:
```json
{
  "RconPassword": "your_password_here",
}
```

## Credits

- Original Metamod:Source version by [Kriax](https://github.com/Salvatore-Als/cs2-fake-rcon)
- CounterStrikeSharp port by [Miksen](https://github.com/mrc4tt)
