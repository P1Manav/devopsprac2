const express = require("express");
const cors = require("cors");
const admin = require("firebase-admin");
const app = express();

app.use(cors());
app.use(express.json());

// FIREBASE SETUP
const serviceAccount = JSON.parse(process.env.FIREBASE_KEY);
admin.initializeApp({
  credential: admin.credential.cert(serviceAccount)
});
const db = admin.firestore();
const users = db.collection("users");

app.get("/api/users", async (req, res) => {
  const snapshot = await users.get();
  res.json(snapshot.docs.map(doc => ({ id: doc.id, ...doc.data() })));
});

app.get("/api/users/:id", async (req, res) => {
  const doc = await users.doc(req.params.id).get();
  if (!doc.exists) return res.status(404).send("User not found");
  res.json({ id: doc.id, ...doc.data() });
});

app.post("/api/users", async (req, res) => {
  const { name, email } = req.body;
  const ref = await users.add({ name, email });
  res.status(201).json({ id: ref.id, name, email });
});

app.put("/api/users/:id", async (req, res) => {
  await users.doc(req.params.id).update(req.body);
  const updated = await users.doc(req.params.id).get();
  res.json({ id: updated.id, ...updated.data() });
});

app.delete("/api/users/:id", async (req, res) => {
  await users.doc(req.params.id).delete();
  res.json({ message: "Deleted" });
});

const PORT = process.env.PORT || 3000;
app.listen(PORT, () => console.log(`Server started on ${PORT}`));
