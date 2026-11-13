# Using the Browser Interface to Control the Prime Sieve API

## Quick Start

### Step 1: Open the Web Interface

1. Navigate to your project folder:
   ```
   C:\Users\administrators1\source\repos\Seive2\
   ```

2. Double-click on **`PrimeSieveController.html`**
   - It will open in your default web browser
   - Works in Chrome, Edge, Firefox, etc.

### Step 2: Configure the API Address

- The **API Server Address** field should show: `http://192.168.1.9:5150`
- If your API is running on a different address, change it here

### Step 3: Start a Job

**Option A: Use Quick Test Buttons**
- Click any quick test button:
  - 🔵 **Small (100K)** - Fast test (1 second)
  - 🟢 **Medium (1M)** - Quick job (few seconds)
  - 🟡 **Large (10M)** - Medium job (10-30 seconds)
  - 🔴 **Huge (100M)** - Large job (minutes)

**Option B: Enter Custom Values**
1. Enter **Max Number** (e.g., `1000000` for 1 million)
2. Enter **Segment Size** (e.g., `100000` for 100 thousand)
3. Click **🚀 Start Job**

### Step 4: Monitor Progress

- Click **📊 Check Status** to see progress
  - See how many primes found
  - View completion percentage
  - Check segment progress

### Step 5: Start Workers

**Important:** The web interface only **starts jobs**. You need workers to process them!

Open PowerShell and run:
```powershell
cd C:\Users\administrators1\source\repos\Seive2\Seive2.Worker
dotnet run -- http://192.168.1.9:5150
```

Or start multiple workers for faster processing!

## Interface Features

### 🚀 Start Job Button
- Creates a new prime sieve job
- Splits work into segments
- Shows job ID and details when successful

### 📊 Check Status Button
- View current job progress
- See real-time completion percentage
- Monitor primes found
- Check elapsed time

### ❤️ Health Check Button
- Verify API is running
- Test connection to server
- Shows API timestamp

### Quick Test Buttons

| Button | Max Number | Segment Size | Expected Time |
|--------|------------|--------------|---------------|
| Small (100K) | 100,000 | 10,000 | ~1 second |
| Medium (1M) | 1,000,000 | 100,000 | ~5 seconds |
| Large (10M) | 10,000,000 | 1,000,000 | ~15 seconds |
| Huge (100M) | 100,000,000 | 10,000,000 | ~2-3 minutes |

## Example: Complete Workflow

### 1. Check API Health
```
1. Open PrimeSieveController.html in browser
2. Click "❤️ Health Check"
3. Should see "✅ API is Healthy"
```

### 2. Start a Test Job
```
1. Click "Medium (1M)" quick test button
2. Click "🚀 Start Job"
3. See success message with Job ID
```

### 3. Start Worker (in PowerShell)
```powershell
cd Seive2.Worker
dotnet run -- http://192.168.1.9:5150
```

### 4. Monitor Progress
```
1. In browser, click "📊 Check Status"
2. Watch progress bar fill up
3. See primes being found in real-time
```

### 5. View Final Results
```
When 100% complete:
- Status: "Completed"
- Total Primes Found: 78,498
- Elapsed Time: 00:00:03.5
```

## What You'll See

### Starting a Job - Success
```
✅ Job Started Successfully!
Job ID: 4fabca7d-fb1a-4f53-9d82-0c0ee8d4921f
Max Number: 1,000,000
Segment Size: 100,000
Total Segments: 10
Started: 7/30/2026, 4:09:22 PM
Now start workers to process the job!
```

### Checking Status - In Progress
```
📊 Job Status: Running
Job ID: 4fabca7d-fb1a-4f53-9d82-0c0ee8d4921f
Max Number: 1,000,000
Total Primes Found: 48,290
Progress: 6 / 10 segments
[Progress Bar: 60.0%]
Pending: 2 | Assigned: 2
Elapsed Time: 00:00:05.234
```

### Checking Status - Completed
```
✅ Job Status: Completed
Job ID: 4fabca7d-fb1a-4f53-9d82-0c0ee8d4921f
Max Number: 1,000,000
Total Primes Found: 78,498
Progress: 10 / 10 segments
[Progress Bar: 100.0%]
Pending: 0 | Assigned: 0
Elapsed Time: 00:00:08.912
Completed: 7/30/2026, 4:09:31 PM
```

## Troubleshooting

### ❌ Cannot Reach API
**Problem:** Red error showing "Cannot Reach API"

**Solution:**
1. Check API is running on http://192.168.1.9:5150
2. Verify the API address in the form is correct
3. Check network/firewall settings

### ❌ Connection Error
**Problem:** "Connection Error" when starting job

**Solution:**
1. Click "❤️ Health Check" first
2. Make sure API shows as healthy
3. Verify API address matches where API is running

### ℹ️ No Job Running
**Problem:** Status shows "No Job Running"

**Solution:**
1. Start a job first using "🚀 Start Job"
2. Wait for success confirmation
3. Then check status

## Advanced Usage

### Find Primes up to 1 Billion
```
1. Max Number: 1000000000
2. Segment Size: 10000000
3. Click "🚀 Start Job"
4. Start 4-8 workers for optimal speed
```

### Monitor Live Progress
```javascript
// Keep clicking "Check Status" every few seconds
// Or use browser console:
setInterval(() => {
	document.querySelector('.btn-secondary').click();
}, 3000); // Check every 3 seconds
```

## Limits and Validation

The interface will validate:
- ✅ Max Number must be at least 2
- ✅ Segment Size must be between 1,000 and 100,000,000
- ✅ Max Number cannot exceed 4 × 10¹⁸

Invalid values will be rejected by the API with error messages.

## Access from Other Devices

### Same Network
1. Open the HTML file on any device on your network
2. Change API address to: `http://192.168.1.9:5150`
3. Click "🚀 Start Job"

### Share the Page
1. Copy `PrimeSieveController.html` to other computers
2. They can control the same API server
3. Multiple people can submit jobs (but only one job runs at a time)

## Tips

- 💡 **Start small** - Use "Small (100K)" first to test everything works
- 💡 **Check health** - Always verify API is healthy before starting big jobs
- 💡 **Monitor progress** - Check status regularly to see how fast it's processing
- 💡 **Multiple workers** - More workers = faster completion (4-8 is optimal)
- 💡 **Don't close browser** - But you can! The job runs on the server, not in the browser

## Summary

The browser interface lets you:
1. ✅ Start prime sieve jobs with custom parameters
2. ✅ Monitor job progress in real-time
3. ✅ Check API health status
4. ✅ Use quick test configurations
5. ✅ See detailed results with progress bars

**Remember:** The browser only controls the API. You still need to run workers to do the actual computation!
