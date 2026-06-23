# **🚀 Lifeguard System User Manual**

## **1\. System Introduction**

**Lifeguard** is a lightweight, enterprise-grade lifeguard system designed specifically for highly memory-constrained VPS environments.

Its primary task is to run in the background, monitoring your **MT4 Trading Terminals** and **HaNodeClient** 24/7. When it detects a crash, freeze, or disconnection in the target programs, Lifeguard automatically kills the zombie processes, restarts the programs, and sends an alert via Telegram.

### **✨ Core Features**

* **Extremely Low Resource Consumption:** Utilises the underlying Windows EventWaitHandle for cross-process heartbeat detection, consuming almost zero CPU and RAM.  
* **Seamless Hot Reload:** Changes to the configuration file (config.json) take effect immediately upon saving, without restarting Lifeguard.  
* **Anti-Loop Restart Mechanism:** Features a boot Grace Period and a Restart Cooldown to prevent the system from falling into an infinite restart loop.  
* **Smart Alert System:** Supports Telegram push notifications. It sends emergency alerts upon crashes and periodically sends "Persistent Crash Reminders" if the issue remains unresolved.  
* **Comprehensive Crash Logging:** Built-in Serilog automatically records all restart events, system status, and exceptions. Max 10MB per log file with automatic rotation.

## **2\. Directory Structure & Deployment**

Please ensure your deployment folder contains the following core files:

* Lifeguard.exe (or the published single executable)  
* config.json (System configuration file)  
* Logs/ (The system will automatically create this folder to store logs)

**Startup Method:** Double-click Lifeguard.exe directly, or add it to the Windows Task Scheduler and set it to "Run at startup".

## **3\. Configuration File (config.json)**

All system behaviours are controlled by config.json. You can open and edit it with Notepad at any time. **The system will automatically apply the new settings once saved.**

{  
  "MT4Instances": {  
    "MT4\_01": "C:\\\\MT4\\\\01\\\\terminal.exe",  
    "MT4\_02": "C:\\\\MT4\\\\02\\\\terminal.exe"  
  },  
  "HaNodeClient": {  
    "ExePath": "C:\\\\NodeClient\\\\HaNodeClient.exe",  
    "HeartbeatTimeoutMs": 15000  
  },  
  "TimeoutMilliseconds": 10000,  
  "InitialDelayMs": 30000,  
  "MaxRetries": 3,  
  "RestartCooldownMs": 15000,  
  "AlertReminderIntervalMs": 3600000,  
  "TelegramBotToken": "YOUR\_TELEGRAM\_BOT\_TOKEN",  
  "TelegramChatId": "YOUR\_CHAT\_ID"  
}

### **Parameter Details**

| Parameter Name | Description | Default / Suggested Value |
| :---- | :---- | :---- |
| MT4Instances | List of MT4 instances to monitor. Format: "Custom\_ID": "Absolute\_Path\_to\_exe". | Set according to the environment |
| HaNodeClient.ExePath | Absolute path to the HaNodeClient executable. Leave blank "" if monitoring is not required. | Set according to the environment |
| HaNodeClient.HeartbeatTimeoutMs | Exclusive heartbeat timeout for HaNodeClient (milliseconds). | 15000 (15s) |
| TimeoutMilliseconds | Global heartbeat timeout for MT4s (milliseconds). Considered dead if no heartbeat is received within this time. | 10000 (10s) |
| InitialDelayMs | **Boot Grace Period**. Time allowed for all software to load upon startup; monitoring and restarts are disabled during this period. | 30000 (30s) |
| MaxRetries | **Maximum Restart Retries**. If consecutive restart failures reach this number, restarts will be suspended and a "Crash Alert" will be sent. | 3 times |
| RestartCooldownMs | **Restart Cooldown**. How long to wait before resuming monitoring after killing a stuck process and restarting it. | 15000 (15s) |
| AlertReminderIntervalMs | **Persistent Crash Reminder Interval**. How often should a Telegram reminder be sent when a software is completely dead. | 3600000 (1 hour) |
| TelegramBotToken | Telegram Bot Token (apply via @BotFather). | \- |
| TelegramChatId | Telegram Group or User ID to receive alerts. | \- |

## **4\. Client Coordination Settings (Heartbeat Mechanism)**

Lifeguard determines process viability by listening to Windows EventWaitHandle. Your monitored endpoints (MT4 EAs and HaNodeClient) **must** periodically trigger the corresponding system events.

💡 **Best Practice:** If the timeout is set to 10 seconds, the monitored endpoint should send a heartbeat every **3 to 5 seconds**.

### **A. MT4 EA Heartbeat Transmission**

The naming convention for MT4 heartbeat events is: Local\\EA\_Heartbeat\_{Your\_MT4\_ID}.

For example, if the ID in config.json is MT4\_01, the event name is Local\\EA\_Heartbeat\_MT4\_01.

**MQL4 Implementation Logic:**

The EA needs to call Windows API (kernel32.dll) functions OpenEventW and SetEvent.

In the EA's OnTimer (e.g., every 3 seconds), execute SetEvent to wake the Lifeguard.

### **B. HaNodeClient Heartbeat Transmission**

The heartbeat event name for HaNodeClient is fixed: Local\\PrimaryServer\_Heartbeat.

**C\# Implementation Example (in HaNodeClient source code):**

using System.Threading;

// Create or open the Event upon program startup  
EventWaitHandle heartbeatEvent \= new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\\PrimaryServer\_Heartbeat");

// Periodically send a heartbeat in a background loop or Timer (e.g., every 5 seconds)  
while(isRunning)  
{  
    heartbeatEvent.Set(); // Send survival signal to Lifeguard  
    Thread.Sleep(5000);  
}

## **5\. Status Monitoring & Logging**

### **Console Window Status**

When the Lifeguard is running, the window title bar dynamically displays the current highest-priority system status:

* Lifeguard: ALL OK \- All systems are operating normally.  
* Lifeguard: ERROR DETECTED \- A timeout or restart occurred within the last minute.  
* Lifeguard: CRITICAL (X EA Offline) \- One or more MT4 instances are completely dead, and restarts have been suspended.  
* Lifeguard: HaNodeClient DOWN \- The node client is completely dead.

### **Log Files (Logs/)**

The system generates Lifeguard\_Log\_YYYYMMDD.txt in the adjacent Logs folder.

* **Daily Records:** Logs configuration reloads, startup information, etc.  
* **Restart Records:** Every process termination (Kill) and restart (Process.Start) is logged in detail with PID and timestamps.  
* **Protection Mechanism:** Maximum log file size is capped at 10MB; it auto-rotates and keeps logs for up to 30 days, ensuring the VPS hard drive never fills up.

## **6\. Frequently Asked Questions (FAQ)**

**Q1: Why did the terminal show a yellow HOT RELOAD message after modifying config.json, but then immediately restart my MT4?**

A: Modifying the config instantly resets the monitoring timers. If you set TimeoutMilliseconds too short, it might trigger a timeout before the next heartbeat arrives. Ensure the heartbeat transmission frequency is at least half of the timeout duration.

**Q2: I am not receiving Telegram alerts. Why?**

A: Please check the following:

1. Ensure there are no typos or trailing spaces in the Token or ChatId in config.json.  
2. Verify that the VPS has outbound internet access.  
3. Check the Logs folder for Telegram notification failed error codes (e.g., 401 means invalid Token, 400 means invalid ChatId).

**Q3: What if I temporarily don't want to monitor a specific MT4 instance?**

A: Open config.json, delete the specific MT4\_XX line, and save the file. Lifeguard will dynamically release that MT4's monitoring thread within seconds, leaving no trace.

**Q4: Can the Lifeguard itself crash?**

A: Lifeguard has a built-in "Global Unhandled Exception" catching mechanism. Even if a severe memory or low-level error occurs, the system will write the cause of death to the log file in the final second before crashing, assisting you in post-mortem debugging.
