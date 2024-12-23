import streamlit as st
import firebase_admin
from firebase_admin import credentials, db
import hashlib
from datetime import date
import os
from dotenv import load_dotenv

load_dotenv()
FIREBASE_URL = os.getenv('FIREBASE_URL')
cred_path = os.getenv('CREDPATH')

# Firebase Configuration
def initialize_firebase():
    if not firebase_admin._apps:
        try:
            cred = credentials.Certificate(cred_path)
            firebase_admin.initialize_app(cred, {
                "databaseURL": FIREBASE_URL
            })
        except Exception as e:
            st.error(f"Failed to initialize Firebase: {str(e)}")
            return False
    return True

def get_db_reference():
    if not initialize_firebase():
        return None
    try:
        return db.reference("users")
    except Exception as e:
        st.error(f"Failed to get database reference: {str(e)}")
        return None

def update(userid):
    ref = get_db_reference()
    if not ref:
        return
    
    try:
        ref.child(userid).update({
            "count":10,
            "last_date":str(date.today())
        })
    except Exception as e:
        st.error(f"Error updating user data: {str(e)}")

def decrease_count(userid):
    ref = get_db_reference()
    if not ref:
        return False
    
    try:
        user_data = ref.child(userid).get()
        if user_data and user_data.get('count', 0) > 0:
            ref.child(userid).update({
                "count": user_data['count'] - 1
            })
            return True
        return False
    except Exception as e:
        st.error(f"Error updating count: {str(e)}")
        return False

def get_remaining_hits(userid):
    ref = get_db_reference()
    if not ref:
        return 0
    
    try:
        user_data = ref.child(userid).get()
        return user_data.get('count', 0) if user_data else 0
    except Exception as e:
        st.error(f"Error getting remaining hits: {str(e)}")
        return 0

def update_date_count(userid):
    ref = get_db_reference()
    if not ref:
        return
    
    try:
        user_data = ref.child(userid).get()
        if user_data:
            last_date = user_data.get('last_date')
            if str(date.today()) != last_date:
                update(userid)
    except Exception as e:
        st.error(f"Error updating date count: {str(e)}")

# Helper Functions
def hash_password(password):
    return hashlib.sha256(password.encode()).hexdigest()

def validate_email(email):
    return email.endswith("@optisolbusiness.com")

def generate_user_id(email):
    username = email.split("@")[0]
    return username.replace(".", "")

def sign_up(email, password):
    if not validate_email(email):
        return "Email must belong to the domain 'optisolbusiness.com'!"
    user_id = generate_user_id(email)
    ref = get_db_reference()
    if not ref:
        return "Failed to get database reference"
    
    try:
        users = ref.get()
        if users and user_id in users:
            return "User already exists!"
        else:
            ref.child(user_id).set({
                "email": email,
                "password": hash_password(password),
                "last_date": str(date.today()),
                "count":10
            })
            return "User registered successfully!"
    except Exception as e:
        st.error(f"Error signing up: {str(e)}")
        return "Error signing up"

def sign_in(email, password):
    if not validate_email(email):
        return "Email must belong to the domain 'optisolbusiness.com'!"
    user_id = generate_user_id(email)
    ref = get_db_reference()
    if not ref:
        return "Failed to get database reference"
    
    try:
        users = ref.get()
        if users and user_id in users:
            stored_password = users[user_id]["password"]
            if stored_password == hash_password(password):
                st.session_state.logged_in = True
                st.session_state.user_id = user_id
                update_date_count(user_id)
                return "Login successful!"
            else:
                return "Incorrect password!"
        else:
            return "User not found!"
    except Exception as e:
        st.error(f"Error signing in: {str(e)}")
        return "Error signing in"

# Initialize Streamlit State
if "logged_in" not in st.session_state:
    st.session_state.logged_in = False

# Main Logic
if not st.session_state.logged_in:
    st.title("Login System")
    page = st.radio("Switch Page", ["Login", "Sign Up"])

    if page == "Login":
        st.header("Login")
        email = st.text_input("Email")
        password = st.text_input("Password", type="password")
        if st.button("Login"):
            if email and password:
                message = sign_in(email, password)
                if message == "Login successful!":
                    st.success(message)
                    # Corrected switch_page call
                    st.switch_page("pages/Main.py")
                else:
                    st.error(message)
            else:
                st.error("Please fill out all fields.")
    else:
        st.header("Sign Up")
        email = st.text_input("Email")
        password = st.text_input("Password", type="password")
        confirm_password = st.text_input("Confirm Password", type="password")
        if st.button("Sign Up"):
            if email and password and confirm_password:
                if password != confirm_password:
                    st.error("Passwords do not match!")
                else:
                    message = sign_up(email, password)
                    if message == "User registered successfully!":
                        st.success(message)
                    else:
                        st.error(message)
            else:
                st.error("Please fill out all fields.")
