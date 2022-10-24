using System.Collections.Concurrent;

namespace Birdae;

public class User {
	public ulong id;
	public ulong guildId;

	public static ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, User>> guildAndIdToUser = new();

	public DateTimeOffset rollRestoreTime;
	public DateTimeOffset claimRestoreTime;
	public bool canClaim;
	public int rollsRemaining;

	public User(ulong id, ulong guildId) {
		this.id = id;
		this.guildId = guildId;

		rollRestoreTime = DateTimeOffset.Now;
		claimRestoreTime = DateTimeOffset.Now;
		canClaim = true;
		rollsRemaining = Settings.instance.rollNumber;
	}

	public bool CanClaim() {
		if (canClaim) return true;
		if (claimRestoreTime >= DateTimeOffset.Now) return false;

		canClaim = true;
		return true;
	}

	public void Claim() {
		canClaim = false;
		claimRestoreTime = DateTimeOffset.Now + Settings.instance.claimRestoreTime;
	}

	public bool CanRoll() {
		if (rollsRemaining > 0) return true;
		if (rollRestoreTime >= DateTimeOffset.Now) return false;

		rollsRemaining = Settings.instance.rollNumber;
		return true;
	}

	public void Roll() {
		if (rollsRemaining == Settings.instance.rollNumber) {
			rollRestoreTime = DateTimeOffset.Now + Settings.instance.rollRestoreTime;
		}

		rollsRemaining--;
	}
}