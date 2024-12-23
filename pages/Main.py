import os
import zipfile
import shutil
from pathlib import Path
import streamlit as st
from langchain_openai import AzureChatOpenAI
from langchain_core.prompts import PromptTemplate
from token_count import check_token_limit  
from dotenv import load_dotenv
import firebase_admin
from firebase_admin import credentials, initialize_app, db

load_dotenv()
cred_path=os.getenv('CREDPATH')
FIREBASE_URL=os.getenv('FIREBASE_URL')
if not firebase_admin._apps:
    cred = credentials.Certificate(cred_path)
    firebase_admin.initialize_app(cred, {"databaseURL": FIREBASE_URL})
def update_date_count(userid):
    ref = db.reference("users")
    user_data = ref.get()  # Fetch all users
    if user_data and userid in user_data:
        if user_data[userid]['last_date']==str(date.today()):
            count= user_data[userid]['count']-1
    ref.child(userid).update({
            "count":count
        })
    return count

# Function to initialize the Azure LLM
def initialize_llm():
    """
    Initializes the Azure OpenAI Large Language Model (LLM).
    """
    #  AzureChatOpenAI(
    #     api_key=os.getenv('AZURE_OPENAI_API_KEY'),
    #     azure_endpoint=os.getenv('AZURE_OPENAI_ENDPOINT'),
    #     api_version=os.getenv('AZURE_OPENAI_API_VERSION'),
    #     azure_deployment=os.getenv('AZURE_OPENAI_DEPLOYMENT'),
    #     temperature=0.7
    # )

# Initialize the LLM
llm =  AzureChatOpenAI(
        api_key=os.getenv('AZURE_OPENAI_API_KEY'),
        azure_endpoint=os.getenv('AZURE_OPENAI_ENDPOINT'),
        api_version=os.getenv('AZURE_OPENAI_API_VERSION'),
        azure_deployment=os.getenv('AZURE_OPENAI_DEPLOYMENT'),
        temperature=0.7
    )

def process_file(file_path):
    try:
        with open(file_path, 'r', encoding='utf-8') as file:
            content = file.read()

        input_prompt = """
        Imagine you are a seasoned software engineer tasked with creating a comprehensive documentation 
        for a given code file. Analyze the provided code file and generate comprehensive documentation 
        in Markdown format. Explain methods, variables, and overall functionality with detailed descriptions.
        Use Mermaid diagrams to visualize class hierarchies and relationships.
        {code}
        """
        
        prompt = PromptTemplate(
            input_variables=["code"],
            template=input_prompt,
        )

        chain = prompt | llm
        res = chain.invoke({"code": content})
        
        file_name_without_extension = os.path.splitext(os.path.basename(file_path))[0]
        output_file_path = f"{file_name_without_extension}.md"
        
        with open(output_file_path, 'w', encoding='utf-8') as output_file:
            output_file.write(res.content)
        return output_file_path

    except Exception as e:
        st.error(f"{e}")

def process_all_files_in_directory(directory_path, output_dir):
    Path(output_dir).mkdir(parents=True, exist_ok=True)
    all_files = [os.path.join(root, file) for root, _, files in os.walk(directory_path) for file in files]
    
    progress_bar = st.progress(0)
    st.info("Documenting files. This may take a while...")
    
    total_files = len(all_files)
    for i, file_path in enumerate(all_files):
        try:
            output_path = process_file(file_path)
            shutil.move(output_path, os.path.join(output_dir, os.path.basename(output_path)))
            progress_bar.progress((i + 1) / total_files)
        except Exception as e:
            st.error(f"Error with {file_path}: {e}")

def clear_old_files(directory):
    """Delete all files and subfolders in a directory."""
    if os.path.exists(directory):
        shutil.rmtree(directory)
    Path(directory).mkdir(parents=True, exist_ok=True)

# Streamlit App
def main():
    st.title("Code Documentation Generator")

    if "download_complete" not in st.session_state:
        st.session_state["download_complete"] = False

    uploaded_file = st.file_uploader("Upload a ZIP file containing your code files", type=["zip"])

    if uploaded_file and not st.session_state["download_complete"]:
        # Define directories
        upload_dir = "uploaded_folder"
        output_dir = "documentation_output"

        # Clear old files before processing
        clear_old_files(upload_dir)
        clear_old_files(output_dir)

        # Unzip the uploaded file
        with zipfile.ZipFile(uploaded_file, 'r') as zip_ref:
            zip_ref.extractall(upload_dir)
        
        # Check token limit before processing
        st.info("Checking the token limit for the uploaded files...")
        if not check_token_limit(upload_dir, encoding_name="cl100k_base"):  # Use the desired encoding name
            st.error(f"Token limit exceeds. Upload fewer files.")
        else:
            st.success("Token limit check passed! The files are within the allowed limit.")
            if st.button("create Documentation"):
                count_limit=update_date_count(userid)
                if count_limit<=0:
                    return st.warning("limit exceed")
                process_all_files_in_directory(upload_dir, output_dir)

                # Create a new ZIP file with the output files
                output_zip_path = "documentation_results.zip"
                with zipfile.ZipFile(output_zip_path, 'w') as zipf:
                    for root, dirs, files in os.walk(output_dir):
                        for file in files:
                            file_path = os.path.join(root, file)
                            zipf.write(file_path, os.path.relpath(file_path, output_dir))
                
                # Allow the user to download the ZIP file
                with open(output_zip_path, "rb") as zip_download:
                    st.download_button(
                        label="Download Documentation as ZIP",
                        data=zip_download,
                        file_name="documentation_results.zip",
                        mime="application/zip",
                        on_click=lambda: st.session_state.update({"download_complete": True})
                    )

    # If the download is complete, show a success message and stop re-running the app
    if st.session_state["download_complete"]:
        st.success("Documentation generation complete. You can close this page.")
        st.stop()
if __name__ == "__main__":
    # Ensure session state keys are initialized before accessing them
    if "download_complete" not in st.session_state:
        st.session_state["download_complete"] = False
    if "logged_in" not in st.session_state:
        st.session_state["logged_in"] = False  # Initialize logged_in if not set

    if st.session_state.logged_in:
        userid = st.session_state.user_id
        main()
        if st.session_state["download_complete"]:
            st.success("Documentation generation complete. You can close this page.")
            st.stop()
    else:
        st.warning("Please login to access the code converter.")