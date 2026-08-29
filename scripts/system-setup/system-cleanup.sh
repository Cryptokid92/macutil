#!/bin/sh -e

. ../common-script.sh

cleanup_system() {
    printf "%b\n" "${YELLOW}Performing system cleanup...${RC}"
    printf "%b\n" "${CYAN}Fixing Mission Control to never rearrange spaces...${RC}"
    defaults write com.apple.dock mru-spaces -bool false

    printf "%b\n" "${CYAN}Emptying Trash...${RC}"
    rm -rf ~/.Trash/*
}

checkEnv
cleanup_system
