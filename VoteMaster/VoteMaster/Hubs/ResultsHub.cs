using Microsoft.AspNetCore.SignalR;

namespace VoteMaster.Hubs
{
    public class ResultsHub : Hub
    {
        public async Task JoinPollGroup(int pollId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Poll_{pollId}");
        }

        public async Task LeavePollGroup(int pollId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Poll_{pollId}");
        }

        public async Task NotifyVoteCast(int pollId, int optionId, int newVoteCount)
        {
            await Clients.Group($"Poll_{pollId}").SendAsync("VoteUpdated", optionId, newVoteCount);
        }

        public async Task NotifyPollStatusChange(int pollId, string status)
        {
            await Clients.Group($"Poll_{pollId}").SendAsync("PollStatusChanged", pollId, status);
        }

        public async Task NotifyPollStart(int pollId, string pollTitle)
        {
            await Clients.All.SendAsync("PollStarted", pollId, pollTitle);
        }

        public async Task NotifyPollEnd(int pollId, string pollTitle)
        {
            await Clients.All.SendAsync("PollEnded", pollId, pollTitle);
        }

        public async Task NotifyVoterParticipation(int pollId, int totalVoters, int votedCount)
        {
            await Clients.Group($"Poll_{pollId}").SendAsync("VoterParticipationUpdated", totalVoters, votedCount);
        }
    }
}
