using System;

namespace RssBandit.ViewModels
{
    /// <summary>
    /// The tiny faked-core seam the presentation layer depends on: the minimal slice of a feed item
    /// the read-state view-models need. The production implementation
    /// (<see cref="NewsItemReadStateAdapter"/>) projects an <c>INewsItem</c>; tests supply a POCO fake.
    /// Keeping the view-models behind this seam keeps them portable and unit-testable without the engine.
    /// </summary>
    public interface IReadStateItem
    {
        /// <summary>The unique identifier of the item.</summary>
        string Id { get; }

        /// <summary>The title of the article or blog entry.</summary>
        string Title { get; }

        /// <summary>The date the article or blog entry was made.</summary>
        DateTime Date { get; }

        /// <summary>
        /// Whether the item has been read. Setting it is the mark-read write-through to the model.
        /// </summary>
        bool BeenRead { get; set; }
    }
}
