const API_BASE = "/api/documents"; // works if nginx proxies /api to backend

function initSearch() {
    const form = document.getElementById("search-form");
    const resultsEl = document.getElementById("results");

    form.addEventListener("submit", async (e) => {
        e.preventDefault();
        resultsEl.innerHTML = "Searching...";
        const query = document.getElementById("search-query").value;

        const res = await fetch(`${API_BASE}/search?query=${encodeURIComponent(query)}`);
        const data = await res.json();

        resultsEl.innerHTML = "";
        data.results.forEach(doc => {
            const li = document.createElement("li");
            li.innerHTML = `<a href="details.html?id=${doc.id}">${doc.fileName}</a> - ${doc.summary}`;
            resultsEl.appendChild(li);
        });
    });
}

function initUpload() {
    const form = document.getElementById("upload-form");
    const status = document.getElementById("status");

    form.addEventListener("submit", async (e) => {
        e.preventDefault();
        const fileInput = document.getElementById("file-input");
        const formData = new FormData();
        formData.append("file", fileInput.files[0]);

        const res = await fetch(`${API_BASE}/upload`, {
            method: "POST",
            body: formData
        });

        if (res.ok) {
            status.innerText = "Upload successful!";
        } else {
            status.innerText = "Upload failed.";
        }
    });
}

function initDetails() {
    const params = new URLSearchParams(window.location.search);
    const id = params.get("id");
    const title = document.getElementById("doc-title");
    const summary = document.getElementById("doc-summary");
    const deleteBtn = document.getElementById("delete-btn");

    async function loadDetails() {
        const res = await fetch(`${API_BASE}/search?query=${id}`);
        const data = await res.json();
        const doc = data.results.find(d => d.id == id);
        if (doc) {
            title.innerText = doc.fileName;
            summary.innerText = doc.summary;
        } else {
            title.innerText = "Not found";
        }
    }

    deleteBtn.addEventListener("click", async () => {
        await fetch(`${API_BASE}/delete?document_id=${id}`, { method: "DELETE" });
        alert("Document deleted!");
        window.location.href = "index.html";
    });

    loadDetails();
}
