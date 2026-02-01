document.getElementById("executeBtn").addEventListener("click", async () => {
  const dbName = document.getElementById("dbName").value || "testdb";
  const query = document.getElementById("query").value.trim();
  const resultCard = document.getElementById("resultCard");
  const resultContent = document.getElementById("resultContent");
  const status = document.getElementById("status");
  const loader = document.getElementById("loader");
  const executeBtn = document.getElementById("executeBtn");

  if (!query) {
    showStatus("Please enter a query.", "error");
    return;
  }

  // Reset UI
  status.style.display = "none";
  resultContent.innerHTML = "";
  loader.style.display = "block";
  executeBtn.disabled = true;

  localStorage.setItem('dbName',dbName);

  try {
    const response = await fetch("http://localhost:5232/query", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ dbName, query }),
    });

    if (!response.ok) {
      const errorData = await response.json();
      throw new Error(
        errorData.error || `HTTP error! status: ${response.status}`,
      );
    }

    const data = await response.json();

    if (typeof data === "string" && data.startsWith("Error:")) {
      renderResult(data);
      showStatus("Execution failed.", "error");
    } else {
      renderResult(data);
      showStatus("Query executed successfully.", "success");
    }
  } catch (error) {
    console.error("Execution error:", error);
    resultContent.innerHTML = `<div class="status-error" style="padding: 1rem; border-radius: 6px;">${error.message}</div>`;
    showStatus("Error executing query.", "error");
  } finally {
    loader.style.display = "none";
    executeBtn.disabled = false;
  }
});

function showStatus(message, type) {
  const status = document.getElementById("status");
  status.textContent = message;
  status.className = `status-message status-${type}`;
  status.style.display = "block";
}

function renderResult(data) {
  const resultContent = document.getElementById("resultContent");

  if (typeof data === "string") {
    resultContent.innerHTML = `<div class="json-raw">${data}</div>`;
    return;
  }

  if (data && data.error) {
    resultContent.innerHTML = `<div class="status-error" style="padding: 1rem; border-radius: 6px;">${data.error}</div>`;
    return;
  }

  if (Array.isArray(data) && data.length > 0) {
    const table = document.createElement("table");
    const thead = document.createElement("thead");
    const tbody = document.createElement("tbody");

    // Headers
    const headers = Object.keys(data[0]);
    const headerRow = document.createElement("tr");
    headers.forEach((header) => {
      const th = document.createElement("th");
      th.textContent = header;
      headerRow.appendChild(th);
    });
    thead.appendChild(headerRow);

    // Rows
    data.forEach((row) => {
      const tr = document.createElement("tr");
      headers.forEach((header) => {
        const td = document.createElement("td");
        // td.textContent = row[header];
        td.textContent = row[header] != null ? row[header] : "NULL";

        tr.appendChild(td);
      });
      tbody.appendChild(tr);
    });

    table.appendChild(thead);
    table.appendChild(tbody);
    resultContent.innerHTML = "";
    resultContent.appendChild(table);
  } else if (Array.isArray(data) && data.length === 0) {
    resultContent.innerHTML =
      '<div style="text-align: center; padding: 1rem;">No rows returned.</div>';
  } else {
    resultContent.innerHTML = `<div class="json-raw">${JSON.stringify(data, null, 2)}</div>`;
  }
}


document.addEventListener("DOMContentLoaded", () => {
  const dbInput = document.getElementById("dbName");

  const savedDbName = localStorage.getItem("dbName");

  dbInput.value = savedDbName || "testdb";

  
  if (!savedDbName) {
    localStorage.setItem("dbName", "testdb");
  }
});

document.getElementById("dbName").addEventListener("input", (e) => {
  localStorage.setItem("dbName", e.target.value);
});
