"""
Unit and Integration Tests for NeuroCompanion Authentication and User Profiles.
Tests:
- User registration with password hashing (PBKDF2)
- Duplicate email rejection (HTTP 409)
- User login with valid credentials
- User login with incorrect credentials (HTTP 401)
- Non-existent user login rejection
- Password recovery request (HTTP 200)
- Authenticated profile retrieval (GET /me)
- Unauthenticated profile rejection (HTTP 401)
- Profile update (PUT /profile)
- Direct Patient Mode entry
"""

import asyncio
import json
import sys
import unittest
import uuid
from pathlib import Path

BACKEND_DIR = str(Path(__file__).resolve().parent.parent)
if BACKEND_DIR not in sys.path:
    sys.path.insert(0, BACKEND_DIR)

from main import app
from telemetry.database import hash_password, verify_password


async def _asgi_request(application, method: str, path: str, body=None, headers: dict = None):
    """Sends a raw ASGI HTTP request and returns (status_code, parsed_body)."""
    body_bytes = b""
    if body is not None:
        body_bytes = json.dumps(body).encode() if isinstance(body, (dict, list)) else body

    raw_headers = [
        (b"content-type", b"application/json"),
        (b"content-length", str(len(body_bytes)).encode()),
    ]
    if headers:
        for k, v in headers.items():
            raw_headers.append((k.lower().encode(), v.encode()))

    scope = {
        "type": "http",
        "asgi": {"version": "3.0"},
        "http_version": "1.1",
        "method": method.upper(),
        "path": path,
        "raw_path": path.encode(),
        "query_string": b"",
        "headers": raw_headers,
        "server": ("127.0.0.1", 8000),
    }
    response_status: list[int] = []
    response_body_parts: list[bytes] = []
    body_sent = False

    async def receive():
        nonlocal body_sent
        if not body_sent:
            body_sent = True
            return {"type": "http.request", "body": body_bytes, "more_body": False}
        await asyncio.sleep(9999)

    async def send(message):
        if message["type"] == "http.response.start":
            response_status.append(message["status"])
        elif message["type"] == "http.response.body":
            response_body_parts.append(message.get("body", b""))

    await application(scope, receive, send)
    raw = b"".join(response_body_parts)
    try:
        parsed = json.loads(raw)
    except Exception:
        parsed = raw.decode(errors="replace")
    return response_status[0] if response_status else 0, parsed


def _post(path: str, body: dict = None, headers: dict = None):
    return asyncio.run(_asgi_request(app, "POST", path, body, headers))


def _get(path: str, headers: dict = None):
    return asyncio.run(_asgi_request(app, "GET", path, None, headers))


def _put(path: str, body: dict = None, headers: dict = None):
    return asyncio.run(_asgi_request(app, "PUT", path, body, headers))


class TestAuthEndpoints(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.test_email = f"test_cg_{uuid.uuid4().hex[:6]}@example.com"
        cls.test_patient_email = f"test_pt_{uuid.uuid4().hex[:6]}@example.com"
        cls.password = "ValidPass123!"

    def test_01_password_hashing(self):
        """Verify PBKDF2 hashing generates salt and verifies accurately."""
        h1, s1 = hash_password("MyPassword!#")
        self.assertEqual(len(h1), 64)
        self.assertEqual(len(s1), 32)
        self.assertTrue(verify_password("MyPassword!#", h1, s1))
        self.assertFalse(verify_password("WrongPassword", h1, s1))

    def test_02_register_caregiver_success(self):
        """Test successful registration of a caregiver account."""
        status_code, data = _post("/api/v1/auth/register", {
            "full_name": "Eleanor Vance",
            "email": self.test_email,
            "password": self.password,
            "role": "caregiver"
        })
        self.assertEqual(status_code, 201)
        self.assertEqual(data["status"], "ok")
        self.assertTrue("token" in data)
        self.assertEqual(data["user"]["email"], self.test_email)
        self.assertEqual(data["user"]["role"], "caregiver")
        self.assertEqual(data["user"]["full_name"], "Eleanor Vance")

    def test_03_register_duplicate_email_fails(self):
        """Test duplicate email registration returns HTTP 409."""
        status_code, data = _post("/api/v1/auth/register", {
            "full_name": "Duplicate User",
            "email": self.test_email,
            "password": "AnotherPassword123!",
            "role": "caregiver"
        })
        self.assertEqual(status_code, 409)
        self.assertIn("already registered", data["detail"])

    def test_04_login_success(self):
        """Test successful login with registered credentials."""
        status_code, data = _post("/api/v1/auth/login", {
            "email": self.test_email,
            "password": self.password
        })
        self.assertEqual(status_code, 200)
        self.assertEqual(data["status"], "ok")
        self.assertTrue("token" in data)
        self.assertEqual(data["user"]["email"], self.test_email)

    def test_05_login_invalid_password_fails(self):
        """Test login fails with incorrect password (HTTP 401)."""
        status_code, data = _post("/api/v1/auth/login", {
            "email": self.test_email,
            "password": "IncorrectPassword999!"
        })
        self.assertEqual(status_code, 401)
        self.assertIn("Invalid email or password", data["detail"])

    def test_06_login_nonexistent_email_fails(self):
        """Test login fails for unknown email (HTTP 401)."""
        status_code, data = _post("/api/v1/auth/login", {
            "email": "ghost_nonexistent_user@example.com",
            "password": "AnyPassword123!"
        })
        self.assertEqual(status_code, 401)

    def test_07_forgot_password_success(self):
        """Test forgot password request for existing account."""
        status_code, data = _post("/api/v1/auth/forgot-password", {
            "email": self.test_email
        })
        self.assertEqual(status_code, 200)
        self.assertEqual(data["status"], "ok")
        self.assertEqual(data["email"], self.test_email)
        self.assertTrue(data["recovery_initiated"])

    def test_08_forgot_password_unknown_email(self):
        """Test forgot password for unregistered email returns HTTP 404."""
        status_code, data = _post("/api/v1/auth/forgot-password", {
            "email": "unknown_email_404@example.com"
        })
        self.assertEqual(status_code, 404)

    def test_09_get_profile_authenticated(self):
        """Test authenticated user profile retrieval using Bearer token."""
        status_code, login_data = _post("/api/v1/auth/login", {
            "email": self.test_email,
            "password": self.password
        })
        token = login_data["token"]

        status_code, user = _get("/api/v1/auth/me", headers={
            "Authorization": f"Bearer {token}"
        })
        self.assertEqual(status_code, 200)
        self.assertEqual(user["email"], self.test_email)
        self.assertEqual(user["full_name"], "Eleanor Vance")

    def test_10_get_profile_unauthenticated_fails(self):
        """Test accessing /me without token returns HTTP 401."""
        status_code, _ = _get("/api/v1/auth/me")
        self.assertEqual(status_code, 401)

    def test_11_update_profile(self):
        """Test updating authenticated user profile."""
        status_code, login_data = _post("/api/v1/auth/login", {
            "email": self.test_email,
            "password": self.password
        })
        token = login_data["token"]

        status_code, updated = _put("/api/v1/auth/profile", {
            "full_name": "Eleanor Vance, RN",
            "patient_association": "P001"
        }, headers={
            "Authorization": f"Bearer {token}"
        })
        self.assertEqual(status_code, 200)
        self.assertEqual(updated["full_name"], "Eleanor Vance, RN")
        self.assertEqual(updated["patient_association"], "P001")

    def test_12_patient_mode_entry(self):
        """Test patient mode entry without requiring credentials."""
        status_code, data = _post("/api/v1/auth/patient-mode", {
            "patient_id": "patient_001"
        })
        self.assertEqual(status_code, 200)
        self.assertEqual(data["status"], "ok")
        self.assertEqual(data["mode"], "PatientMode")
        self.assertEqual(data["patient_id"], "patient_001")
        self.assertIsNone(data["active_support_tier"])
        self.assertEqual(data["carl_instruction_style"], "concise")


if __name__ == "__main__":
    unittest.main()
