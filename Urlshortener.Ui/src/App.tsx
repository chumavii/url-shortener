import { useState, type FormEvent } from "react";

const BASE_URL = import.meta.env.VITE_API_BACKEND_URL || '';
if (!BASE_URL) {
    console.error('VITE_API_BACKEND_URL is not set');
}

interface ShortenResponse {
    shortUrl: string;
}
interface ExpandResponse {
    originalUrl: string;
}

function App() {
    const [activeTab, setActiveTab] = useState<"shorten" | "expand">("shorten");
    const [input, setInput] = useState("");
    const [result, setResult] = useState("");
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState("");

    const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
        e.preventDefault();
        setError("");
        setResult("");
        setLoading(true);

        try {
            if (activeTab === "shorten") {
                // Shorten URL
                const response = await fetch(`${BASE_URL}`, {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ originalUrl: input }),
                });

                if (!response.ok) throw new Error(await response.text());
                const data: ShortenResponse = await response.json();
                setResult(data.shortUrl);
            } else {
                // Expand short URL
                const urlPattern = /\/?([^\/\s]+)$/;
                const match = input.trim().match(urlPattern);
                const shortCode = match?.[1];
                if (!shortCode) throw new Error("Invalid short URL format");

                const response = await fetch(`${BASE_URL}/${shortCode}`);
                if (!response.ok) throw new Error(await response.text());
                const data: ExpandResponse = await response.json();

                setResult(data.originalUrl);
            }
        } catch (err: any) {
            setError(err.message ?? "Something went wrong");
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="h-screen w-screen flex flex-col justify-center items-center bg-gradient-to-br from-gray-950 via-gray-900 to-gray-950 text-gray-100 px-4">

            {/* --- Logo Section --- */}
            <div className="flex flex-col items-center mb-6">
                <img
                    src="/logo.png"
                    alt="URL Shortener Logo"
                    className="h-14"
                />
            </div>
            <div className="w-full max-w-md bg-gray-900/70 backdrop-blur-xl p-8 rounded-2xl shadow-2xl border border-gray-800">

                {/* Tabs */}
                <div className="flex mb-6 border-b border-gray-700">
                    {["shorten", "expand"].map((tab) => (
                        <button
                            key={tab}
                            onClick={() => {
                                setActiveTab(tab as "shorten" | "expand");
                                setInput("");
                                setResult("");
                                setError("");
                            }}
                            className={`flex-1 py-2 text-center font-medium transition cursor-pointer ${activeTab === tab
                                ? "text-blue-400 border-b-2 border-blue-400"
                                : "text-gray-500 hover:text-gray-300"
                                }`}
                        >
                            {tab === "shorten" ? "Shorten URL" : "Expand URL"}
                        </button>
                    ))}
                </div>

                <form onSubmit={handleSubmit} className="space-y-5">
                    <div>
                        <label
                            htmlFor="input"
                            className="block text-sm font-medium text-gray-300 mb-2"
                        >
                            {activeTab === "shorten"
                                ? "Enter a long URL:"
                                : "Enter a short URL:"}
                        </label>
                        <input
                            type="url"
                            id="input"
                            value={input}
                            onChange={(e) => setInput(e.target.value)}
                            required
                            placeholder={
                                activeTab === "shorten"
                                    ? "https://example.com/long/path"
                                    : "https://your-short-url/abc123"
                            }
                            className="w-full px-4 py-2 bg-gray-800 border border-gray-700 rounded-lg text-gray-100 placeholder-gray-500 focus:ring-2 focus:ring-blue-500 focus:border-blue-500 outline-none transition"
                        />
                    </div>

                    <button
                        type="submit"
                        disabled={loading}
                        className="w-full py-2 font-semibold rounded-lg bg-blue-600 hover:bg-blue-500 text-white transition disabled:opacity-50 cursor-pointer"
                    >
                        {loading
                            ? activeTab === "shorten"
                                ? "Shortening..."
                                : "Expanding..."
                            : activeTab === "shorten"
                                ? "Shorten URL"
                                : "Expand URL"}
                    </button>
                </form>

                {error && (
                    <p className="mt-4 text-red-400 text-center font-medium">{error}</p>
                )}

                {result && (
                    <div className="mt-6 text-center">
                        <p className="text-gray-400 mb-2">
                            {activeTab === "shorten"
                                ? "Shortened URL:"
                                : "Original URL:"}
                        </p>
                        <a
                            href={result}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="text-blue-400 hover:underline font-semibold break-all"
                        >
                            {result}
                        </a>
                    </div>
                )}
            </div>

            <footer className="absolute bottom-6 text-sm text-gray-500">
                {new Date().getFullYear()} URL Shortener
            </footer>
        </div>
    );
}

export default App;
