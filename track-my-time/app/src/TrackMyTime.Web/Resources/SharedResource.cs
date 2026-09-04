namespace TrackMyTime.Web;

/// <summary>Marker type only - exists so <c>IStringLocalizer&lt;SharedResource&gt;</c> has
/// somewhere to anchor to. All UI strings live in SharedResource.resx (English, the default) and
/// SharedResource.da.resx (Danish), shared across every page rather than split per-page, since
/// several terms (e.g. "Client", "Project", "Hours") repeat across pages and should stay
/// consistently translated.
///
/// Deliberately namespaced <c>TrackMyTime.Web</c>, matching this type's own name with no
/// ".Resources" segment - even though the file lives in the Resources/ folder - because that's
/// the manifest resource name this SDK actually embeds SharedResource.resx under (verified via
/// `strings` on the built dll: "TrackMyTime.Web.SharedResource", not
/// "TrackMyTime.Web.Resources.SharedResource"). IStringLocalizer resolves resources by this
/// type's namespace-qualified name, so the two must match; see the AddLocalization() call in
/// Program.cs for the other half of this.</summary>
public sealed class SharedResource;
