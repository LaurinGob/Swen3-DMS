const API_BASE = "/api/documents"; // works if nginx proxies /api to backend

// ---------------- Upload ----------------
export function initUpload() {
    const form = document.getElementById("upload-form");
    const feedback = document.getElementById("upload-feedback");

    form.addEventListener("submit", async (e) => {
        e.preventDefault();
        const fileInput = document.getElementById("file-input");

        if (!fileInput.files.length) {
            feedback.innerText = "Please select a file to upload.";
            feedback.style.color = "#B684EB";
            return;
        }

        const formData = new FormData();
        formData.append("file", fileInput.files[0]);

        try {
            const res = await fetch(`${API_BASE}/upload`, {
                method: "POST",
                body: formData
            });

            if (res.ok) {
                feedback.innerText = "Upload successful!";
                feedback.style.color = "#EDED68";
                fileInput.value = ""; // reset input
                loadAllDocuments(); // refresh table
            } else {
                feedback.innerText = "Upload failed!";
                feedback.style.color = "#9A8AAB";
            }
        } catch (err) {
            feedback.innerText = "Error uploading file.";
            feedback.style.color = "#5D4F6B";
        }
    });
}

// ---------------- Search & Table ----------------
export function initSearch() {
    const form = document.getElementById("search-form");
    form.addEventListener("submit", async (e) => {
        e.preventDefault();
        const query = document.getElementById("search-query").value;
        loadAllDocuments(query);
    });

    // Load all documents on page load
    loadAllDocuments();
}

async function loadAllDocuments(query = "") {
    const tableBody = document.querySelector("#results-table tbody");
    tableBody.innerHTML = "<tr><td colspan='3'>Loading...</td></tr>";

    try {
        const res = await fetch(`${API_BASE}/search?query=${encodeURIComponent(query)}`);
        const data = await res.json();
        tableBody.innerHTML = "";

        if (!data.results.length) {
            tableBody.innerHTML = "<tr><td colspan='3'>No documents found.</td></tr>";
            return;
        }

        data.results.forEach(doc => {
            const tr = document.createElement("tr");

            tr.innerHTML = `
                <td>${doc.fileName}</td>
                <td>${doc.summary}</td>
                <td>
                    <button class="view-btn" data-id="${doc.id}">View Details</button>
                    <button class="delete-btn" data-id="${doc.id}">Delete</button>
                </td>
            `;
            tableBody.appendChild(tr);
        });

        // Add event listeners
        document.querySelectorAll(".view-btn").forEach(btn => {
            btn.addEventListener("click", () => {
                const id = btn.getAttribute("data-id");
                window.location.href = `details.html?id=${id}`;
            });
        });

        document.querySelectorAll(".delete-btn").forEach(btn => {
            btn.addEventListener("click", async () => {
                const id = btn.getAttribute("data-id");
                if (!confirm("Are you sure you want to delete this document?")) return;

                const res = await fetch(`${API_BASE}/delete?document_id=${id}`, { method: "DELETE" });
                if (res.ok) {
                    alert("Document deleted!");
                    loadAllDocuments();
                } else {
                    alert("Failed to delete document.");
                }
            });
        });

    } catch (err) {
        tableBody.innerHTML = "<tr><td colspan='3'>Error loading documents.</td></tr>";
    }
}

// ---------------- Details Page ----------------
export function initDetails() {
    const params = new URLSearchParams(window.location.search);
    const id = params.get("id");
    const titleEl = document.getElementById("doc-title");
    const summaryEl = document.getElementById("summary-input");
    const updateBtn = document.getElementById("update-btn");
    const deleteBtn = document.getElementById("delete-btn");
    const feedback = document.getElementById("feedback");

    async function loadDetails() {
        const res = await fetch(`${API_BASE}/search?query=${id}`);
        const data = await res.json();
        const doc = data.results.find(d => d.id == id);
        if (doc) {
            titleEl.innerText = doc.fileName;
            summaryEl.value = doc.summary;
        } else {
            titleEl.innerText = "Document not found";
        }
    }

    updateBtn.addEventListener("click", async () => {
        const content = summaryEl.value.trim();
        if (!content) {
            feedback.innerText = "Summary cannot be empty.";
            feedback.style.color = "#B684EB";
            return;
        }

        const res = await fetch(`${API_BASE}/update`, {
            method: "PUT",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ documentId: parseInt(id), content })
        });

        if (res.ok) {
            feedback.innerText = "Update successful!";
            feedback.style.color = "#EDED68";
        } else {
            feedback.innerText = "Update failed.";
            feedback.style.color = "#9A8AAB";
        }
    });

    deleteBtn.addEventListener("click", async () => {
        if (!confirm("Are you sure you want to delete this document?")) return;
        const res = await fetch(`${API_BASE}/delete?document_id=${id}`, { method: "DELETE" });
        if (res.ok) {
            alert("Document deleted!");
            window.location.href = "index.html";
        } else {
            alert("Failed to delete document.");
        }
    });

    loadDetails();
}
