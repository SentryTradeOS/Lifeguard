🛡️ Lifeguard

Lifeguard is an ultra-lightweight, high-performance monitoring system and infrastructure monitoring tool specifically engineered for MT4 (MetaTrader 4) environments and automated trading systems.

Compiled with .NET Native AOT (Ahead-Of-Time), it operates with a virtually non-existent memory footprint, making it the perfect choice for high-availability VPS environments where every megabyte counts.

✨ Key Features

🚀 Native AOT Performance

Fully compiled to native machine code.

No .NET runtime required on the target VPS.

Extremely low memory consumption (~4.5MB).

🛡️ Global Single-Instance Protection

Utilises a machine-level OS Mutex (Global\LifeguardSystem_SingleInstance_Mutex).

Prevents duplicate instances from running across different Windows user sessions, Remote Desktop (RDP) connections, or background services.

Ensures zero-conflict monitoring and prevents duplicated alert dispatches.

📉 MT4 Smart Recovery & Heartbeat Guard

Monitors the responsiveness and execution status of MT4 processes.

Detects deadlocks, freezes, and crashes, triggering clean, automated recovery processes.

📡 Ecosystem Sync (HaNodeClient)

Seamlessly monitors node-client health states.

Pre-configured to work hand-in-hand with the upcoming HaNodeClient for failover high-availability configurations.

💬 Instant Telegram Alerts

Immediate dispatch of warnings and critical crash events directly to your Telegram channel.

Periodic "All Clear" heartbeat status updates.

🪵 Production-Grade Logging

Backed by Serilog with automated 10MB daily rolling limits and 30-day log-retention policies.

🗺️ System Architecture
<img width="1024" height="559" alt="image" src="https://github.com/user-attachments/assets/4d185d20-3b8b-4fef-9c72-a91282309665" />



📦 Quick Start

1. Prerequisites

OS: Windows Server 2012+ or Windows 10/11 (64-bit).

Dependencies: None! Thanks to Native AOT, no .NET runtimes or SDKs are required.

2. Installation

Go to the Releases page.

Download the latest Lifeguard_Core_v1.1.0_win-x64.zip.

Extract the contents to your preferred folder on your VPS (e.g., C:\TradeSentry\Lifeguard).

3. Configuration

Rename config_example.json to config.json and customise your settings.

4. Running the Lifeguard

Double-click Lifeguard.exe or run it via Command Prompt:

Lifeguard.exe


⚙️ How Single-Instance Protection Works

To prevent conflicting API requests, resource lockups, and dual-alerting, Lifeguard enforces a strict single-instance constraint:

First Instance: Obtains the global OS mutex and begins background polling.

Subsequent Instances: * Detect that the mutex is already held.

Log a warning inside the terminal: [WARNING] The Lifeguard system is already running in the background!

Safely auto-terminate after a 3-second visual countdown to let the operator see the status.

🪵 Log Management

Logs are generated in the ./Logs folder using the following format:
Logs/Lifeguard_Log_YYYYMMDD.txt

The log engine automatically trims files exceeding 10MB and retains archives up to 30 days to prevent your VPS disk from running out of space.

💖 Support & Sponsorship

If Lifeguard has saved your trade environment or helped secure your automated infrastructure, consider supporting its development!

You can sponsor this project via the Sponsor button on the right, or buy us a coffee directly via PayPal:

👉 Support SentryTradeOS on https://buymeacoffee.com/ccmeng

📄 License

This project is licensed under the MIT License - see the LICENSE file for details.
