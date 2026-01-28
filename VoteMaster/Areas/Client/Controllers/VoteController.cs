// Disable voting function if user has already voted
if (UserHasVoted(userId)) {
    TempData["Message"] = "You are done voting.";
    return RedirectToAction("VotingCompleted");
}

// Add option to discard previous votes and revote
if (TempData["Revote"] != null && (bool)TempData["Revote"] == true) {
    DiscardPreviousVotes(userId);
    TempData["Message"] = "Your previous votes have been discarded. You can vote again.";
}