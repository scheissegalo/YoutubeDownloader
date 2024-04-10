#!/bin/bash

# Function to check if a command exists
command_exists() {
    command -v "$1" &> /dev/null
}

# Function for installing packages on Debian-based systems
install_debian() {
    sudo apt-get update
    sudo apt-get install -y xclip yt-dlp ffmpeg
}

# Function for installing packages on Red Hat-based systems
install_redhat() {
    sudo dnf install -y xclip yt-dlp ffmpeg
}

# Check for each command and install if not found
for cmd in xclip yt-dlp ffmpeg; do
    if ! command_exists $cmd; then
        echo "$cmd not found, will attempt to install."

        # Identify the package manager and install missing packages
        if command_exists apt-get; then
            install_debian
        elif command_exists dnf; then
            install_redhat
        else
            echo "Unsupported package manager. Please install $cmd manually."
            exit 1
        fi

        # Break after attempting to install dependencies to avoid repetition
        break
    fi
done

echo "All required dependencies are installed."
