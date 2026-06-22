🚀 MT4-Lifeguard H/A: Enterprise-Grade High Availability Solution

The solution to empower MT4 with "Auto-Recovery" and "Distributed Split-Brain Prevention" capabilities.

MT4-Lifeguard H/A is a High Availability (HA) framework designed specifically for professional forex traders. Through a 1 Arbiter + 2 Node Clients (1A+2N) architecture, it resolves MT4 crashes, VPS disconnections, and the most fatal issue of all: "Split-Brain" (duplicate orders).

✨ Core Highlights
🛡️ 1A+2N Distributed Arbitration: Introduces an Arbiter mechanism. During network fluctuations, the Arbiter determines the singular Master node, 100% eliminating the tragedy of simultaneous order execution across dual machines.

🔌 Universal EA Compatibility: No EA code modifications required. By toggling the MT4 AutoTrading switch at the system level, it perfectly supports all closed-source EAs purchased from the MQL5 Market.

⚡ Win32 Low-Level Synchronization: Utilizes local Win32Event core synchronization for microsecond-level detection of EA survivability, with near-zero resource consumption.

🐧 Cross-Platform Deployment: Supports Linux for the Arbiter (lightweight and stable) and Windows for the trading nodes (highest compatibility), forging the most robust hybrid cloud architecture.

🧠 Epoch Persistence: Features Epoch incrementation and state persistence, enabling instant recovery of the arbitration logic even after a system reboot.

🛡️ Dual Health Checks: Fully supports Uptime Kuma endpoint monitoring.

Port 5000: Ensures the Linux Arbiter service is online.

Port 5001: Ensures the Windows execution nodes are online.

Built-in Health Check Endpoints: Natively supports text/plain health check endpoints, perfectly adapting to third-party monitoring software like Uptime Kuma. Simply configure the keyword "Healthy" to attain enterprise-grade visual monitoring safeguards.

The visualization details the relationships and responsibilities within the 1A+2N Cluster:

[Linux] Arbiter-ha (The Brain): Centrally manages epoch data, lease issuance, and master declarations to prevent "Split-Brain" scenarios.

[Windows] NodeClients (The Workers): Shows the two nodes (x2) executing negotiation and directly controlling the MT4 AutoTrading switches.

[Windows] Watchdog (The Guard): Monitors heartbeats from both NodeClients and their associated MT4/EAs, with defined actions to trigger restarts and send alerts via the Telegram Bot API.

<img width="2816" height="1536" alt="HA Map" src="https://github.com/user-attachments/assets/1718974d-d16e-49a8-840e-35baa7e8867d" />



🛠️ Tech Stack
Language: C# (.NET 10.0)

Protocol: TCP (Cross-node) / Win32 API (Local IPC)

Platform: Linux (Debian/Ubuntu), Windows 10/11/Server 2022+

Messaging: Telegram Bot API

💖 Sponsorship & Support
This project is personally developed and maintained by me. If you find that this tool has helped you avoid significant trading risks or provided value to your automated trading setup, please consider supporting my continuous optimisation efforts through:

GitHub Sponsors: Click the Sponsor button at the top of the page (Recommended).

Coffee: https://buymeacoffee.com/ccmeng

Your sponsorship will be used for:

Maintaining multiple off-site VPS environments for testing.

Developing a more intuitive Web monitoring dashboard.

Continuously optimising the robustness of the Linux arbitration algorithm.

📝 Disclaimer
This project is provided solely as a system stability aid and does not guarantee any trading profits. Forex trading involves high risk; please deploy this tool only after conducting thorough testing.

💡 Message to Developers
If you are interested in distributed systems, C# optimisation, or forex automated trading control, feel free to submit an Issue or Pull Request!
