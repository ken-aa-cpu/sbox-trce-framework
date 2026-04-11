namespace Trce.Kernel.Storage;

/// <summary>
/// A standardized structure representing a stack of items within the TRCE framework.
/// Implemented as a readonly struct to ensure immutability and high-performance stack operations.
/// </summary>
public readonly struct ItemStack
{
	/// <summary>
	/// The unique identifier of the item, corresponding to a TrceItemDefinition.
	/// </summary>
	public string ItemId { get; }

	/// <summary>
	/// The quantity of items in this stack.
	/// </summary>
	public int Amount { get; }

	/// <summary>
	/// Optional JSON string containing item-specific metadata (e.g., durability, custom tags).
	/// </summary>
	public string Metadata { get; }

	/// <summary>
	/// Constructor for creating an ItemStack.
	/// </summary>
	public ItemStack( string itemId, int amount, string metadata = "" )
	{
		ItemId = itemId;
		Amount = amount;
		Metadata = metadata ?? string.Empty;
	}

	/// <summary>
	/// Returns true if the stack is null, has no quantity, or has an empty ItemId.
	/// </summary>
	public bool IsEmpty => Amount <= 0 || string.IsNullOrEmpty( ItemId );

	/// <summary>
	/// Merges another ItemStack into this one. 
	/// Both stacks must have the same ItemId for a successful merge.
	/// </summary>
	/// <param name="other">The other stack to merge into this one.</param>
	/// <returns>A new ItemStack representing the merged state.</returns>
	public ItemStack Merge( ItemStack other )
	{
		if ( other.IsEmpty ) return this;
		if ( IsEmpty ) return other;

		if ( ItemId != other.ItemId )
		{
			// If IDs differ, merge is logically invalid. 
			// In TRCE, we return the original stack to prevent data corruption.
			return this;
		}

		return new ItemStack( ItemId, Amount + other.Amount, Metadata );
	}

	/// <summary>
	/// Splits the stack into two separate parts.
	/// </summary>
	/// <param name="count">The number of items to split off into a new stack.</param>
	/// <returns>A tuple containing (SplitResult, Remainder).</returns>
	public (ItemStack split, ItemStack remainder) Split( int count )
	{
		if ( count <= 0 ) return (default, this);
		if ( count >= Amount ) return (this, default);

		var split = new ItemStack( ItemId, count, Metadata );
		var remainder = new ItemStack( ItemId, Amount - count, Metadata );

		return (split, remainder);
	}
}
