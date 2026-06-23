# **💓 Lifeguard \- EA Companion User Manual**

## **1\. Introduction**

The **Lifeguard EA Companion** is a specialised, ultra-lightweight Expert Advisor (EA) designed to work seamlessly with the Lifeguard system.

Since MT4 terminals are prone to UI freezing or silent disconnections without actually crashing (Zombie state), simple process monitoring is insufficient. This EA solves that by emitting a periodic "heartbeat" directly to the Windows OS layer using native API calls (kernel32.dll). If the Lifeguard stops receiving this heartbeat, it knows the terminal has frozen and will safely restart it.

### **✨ Key Features**

* **Zero Overhead:** Utilises Windows EventWaitHandle for cross-process communication. It consumes virtually zero CPU or RAM and will not interfere with your trading EAs.  
* **Smart Error Handling:** Prevents log flooding (spam) if the heartbeat temporarily fails to send.  
* **Safe Resource Management:** Automatically closes and releases Windows system handles when the EA is removed or the terminal shuts down.

## **2\. Installation & Setup**

### **Step 1: Install the EA**

1. Open your MT4 terminal.  
2. Go to File \-\> Open Data Folder.  
3. Navigate to the MQL4\\Experts folder.  
4. Paste the EA source code file (e.g., Lifeguard\_Emitter.mq4) here.  
5. Open MetaEditor, find the file, and click **Compile** (or simply refresh the Navigator panel in MT4).

### **Step 2: Attach to a Chart**

1. Open **any** chart in your MT4 terminal. (It does not matter which symbol or timeframe).  
2. Drag and drop the Lifeguard EA onto the chart.  
3. In the EA properties window, ensure the Allow DLL imports checkbox is ticked under the "Common" tab as well.  
4. Set your parameters in the "Inputs" tab (see section below) and click OK.  
5. Regardless of whether the top right corner of the chart displays a happy or sad face, this EA will send a timely heartbeat.

## **3\. Configuration Parameters (Inputs)**

When attaching the EA to a chart, you will see the following parameters in the "Inputs" tab:

| Parameter | Description | Default Value | Recommended |
| :---- | :---- | :---- | :---- |
| **InstanceID** | The unique identifier for this specific MT4 terminal. **This MUST match the key used in the Lifeguard's config.json.** | MT4\_01 | UNIQUE MUST match the Lifeguard’s config.json |
| **HeartbeatInterval** | How often (in seconds) the EA sends a heartbeat signal to the Lifeguard. | 3 | 3 to 5 seconds |

⚠️ **Important Mapping Rule:**

If your Lifeguard’s config.json has an entry like:-

"MT4\_02": "C:\\\\MT4\\\\02\\\\terminal.exe"

Then the InstanceID for the EA running inside that specific terminal **MUST** be set to:-

MT4\_02

## **4\. How to Verify It's Working**

Once the EA is successfully running, you should check the MT4 **"Experts"** tab at the bottom of the terminal.

**✅ Successful Output:**

You should see a message similar to:

Lifeguard Event registered successfully: Local\\EA\_Heartbeat\_MT4\_01 | Handle: 1234

*(This means the EA has successfully bridged the connection to the Windows OS).*

**❌ Error Outputs & Troubleshooting:**

* **Initialisation failed: Please check 'Allow DLL imports' in EA settings.**  
  * **Cause:** Forgot to enable DLL imports.  
  * **Fix:** Reapply the EA, go to the Common tab, and tick "Allow DLL imports".  
* **Failed to create Event. Windows System Error Code:**  
  * **Cause:** The EA lacks Windows administrative privileges, or the OS blocked the event creation.  
  * **Fix:** Ensure your MT4 is not installed in a heavily restricted folder (like Program Files). Running MT4 in Portable Mode (/portable) usually prevents this.  
* **Failed to trigger SetEvent. Windows System Error Code:**  
  * **Cause:** The event was created, but the periodic pulse failed.  
  * **Fix:** The EA will only print this once to avoid lagging your terminal. If the Lifeguard is still recognising the MT4 as online, you can safely ignore this. If the Lifeguard restarts the MT4, check your CPU usage, as severe lag might delay the timer.

## **5\. Uninstallation / Updates**

To stop the heartbeat:

1. Close the Lifeguard console.  
2. Right-click the chart where the EA is attached.  
3. Select: Expert Advisors \-\> Remove.  
4. The EA will instantly release the Windows handle (logging: Lifeguard Event handle closed and released safely) and stop emitting signals. If the console doesn’t close in step 1, it will assume the MT4 has crashed and initiate a restart based on its configuration.  
5. Edit config.json based on the new requirement.  
6. Restart Lifeguard.

*(If you are intentionally doing maintenance, it is recommended to close the Lifeguard console first before removing the EA.)*
