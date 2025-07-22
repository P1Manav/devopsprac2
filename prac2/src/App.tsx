import { useState } from "react";
import axios from "axios";

const API = "https://devopsprac2.onrender.com";

export default function App() {
  const [method, setMethod] = useState("GET");
  const [userId, setUserId] = useState("");
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [response, setResponse] = useState<null | string | any>(null);


  const handleSubmit = async () => {
    try {
      let res;
      switch (method) {
        case "GET":
          res = userId ? await axios.get(`${API}/${userId}`) : await axios.get(API);
          break;
        case "POST":
          res = await axios.post(API, { name, email });
          break;
        case "PUT":
          res = await axios.put(`${API}/${userId}`, { name, email });
          break;
        case "DELETE":
          res = await axios.delete(`${API}/${userId}`);
          break;
        default:
          return;
      }
      setResponse(res.data);
    } catch (err) {
      if (axios.isAxiosError(err)) {
        setResponse(err.response?.data || "Request failed");
      } else {
        setResponse("An unknown error occurred");
      }
    }
  };

  return (
    <div style={{ padding: 20, fontFamily: "sans-serif" }}>
      <h2>CRUD Tester</h2>

      <label>Method: </label>
      <select value={method} onChange={(e) => setMethod(e.target.value)}>
        <option value="GET">GET</option>
        <option value="POST">POST</option>
        <option value="PUT">PUT</option>
        <option value="DELETE">DELETE</option>
      </select>

      <div style={{ marginTop: 10 }}>
        <label>User ID: </label>
        <input
          type="text"
          value={userId}
          onChange={(e) => setUserId(e.target.value)}
          placeholder="optional for GET / required for PUT, DELETE"
        />
      </div>

      {(method === "POST" || method === "PUT") && (
        <>
          <div>
            <label>Name: </label>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="name"
            />
          </div>
          <div>
            <label>Email: </label>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="email"
            />
          </div>
        </>
      )}

      <button onClick={handleSubmit} style={{ marginTop: 10 }}>Send Request</button>

      <div style={{ marginTop: 20 }}>
        <h3>Response:</h3>
        <pre style={{ backgroundColor: "#000", padding: 10, color: "#fff" }}>
          {JSON.stringify(response, null, 2)}
        </pre>
      </div>
    </div>
  );
}
