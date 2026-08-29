#!/bin/sh -e

. ../common-script.sh

removeAnimations() {
    printf "%b\n" "${YELLOW}Reducing motion and animations on macOS...${RC}"

    printf "%b\n" "${CYAN}Setting reduce motion preference...${RC}"
    defaults write com.apple.universalaccess reduceMotion -bool true

    printf "%b\n" "${CYAN}Disabling window animations...${RC}"
    defaults write NSGlobalDomain NSAutomaticWindowAnimationsEnabled -bool false

    printf "%b\n" "${CYAN}Speeding up window resize animations...${RC}"
    defaults write NSGlobalDomain NSWindowResizeTime -float 0.001

    printf "%b\n" "${CYAN}Disabling smooth scrolling...${RC}"
    defaults write NSGlobalDomain NSScrollAnimationEnabled -bool false

    printf "%b\n" "${CYAN}Disabling window open/close animations...${RC}"
    defaults write NSGlobalDomain NSAutomaticWindowAnimationsEnabled -bool false

    printf "%b\n" "${CYAN}Disabling Quick Look animations...${RC}"
    defaults write -g QLPanelAnimationDuration -float 0

    printf "%b\n" "${CYAN}Disabling Finder Info window animations...${RC}"
    defaults write com.apple.finder DisableAllAnimations -bool true

    printf "%b\n" "${CYAN}Speeding up Mission Control animations...${RC}"
    defaults write com.apple.dock expose-animation-duration -float 0.1
    defaults write com.apple.dock expose-group-apps -bool true

    printf "%b\n" "${CYAN}Speeding up Launchpad animations...${RC}"
    defaults write com.apple.dock springboard-show-duration -float 0.1
    defaults write com.apple.dock springboard-hide-duration -float 0.1

    printf "%b\n" "${CYAN}Disabling dock hiding animations...${RC}"
    defaults write com.apple.dock autohide-time-modifier -float 0
    defaults write com.apple.dock autohide-delay -float 0

    printf "%b\n" "${CYAN}Disabling Mail animations...${RC}"
    defaults write com.apple.mail DisableReplyAnimations -bool true
    defaults write com.apple.mail DisableSendAnimations -bool true

    printf "%b\n" "${CYAN}Disabling text field zoom animations...${RC}"
    defaults write NSGlobalDomain NSTextShowsControlCharacters -bool true

    printf "%b\n" "${GREEN}Motion and animations have been reduced.${RC}"
    killall Dock
    printf "%b\n" "${YELLOW}Dock Restarted.${RC}"
}

checkEnv
removeAnimations
