require("dotenv").config();
const express = require("express");
const cors = require("cors");
const admin = require("firebase-admin");

const app = express();
app.use(cors());
app.use(express.json());

const serviceAccount = {
  type: process.env.FIREBASE_TYPE,
  project_id: process.env.FIREBASE_PROJECT_ID,
  private_key_id: process.env.FIREBASE_PRIVATE_KEY_ID,
  private_key: process.env.FIREBASE_PRIVATE_KEY.replace(/\\n/g, '\n'),
  client_email: process.env.FIREBASE_CLIENT_EMAIL,
  client_id: process.env.FIREBASE_CLIENT_ID,
  auth_uri: process.env.FIREBASE_AUTH_URI,
  token_uri: process.env.FIREBASE_TOKEN_URI,
  auth_provider_x509_cert_url: process.env.FIREBASE_AUTH_PROVIDER_X509_CERT_URL,
  client_x509_cert_url: process.env.FIREBASE_CLIENT_X509_CERT_URL,
  universe_domain: process.env.FIREBASE_UNIVERSE_DOMAIN
};
console.log("Loaded env vars:", {
  type: process.env.FIREBASE_TYPE,
  project_id: process.env.FIREBASE_PROJECT_ID,
  client_email: process.env.FIREBASE_CLIENT_EMAIL,
  private_key_present: !!process.env.FIREBASE_PRIVATE_KEY
});

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
