/** KubChat app icon (cat face, transparent background). Same artwork as the favicon; decorative. */
export default function BrandLogo({ className }: { className?: string }) {
  return <img className={className} src="/kubchat-icon.png" alt="" width={256} height={256} draggable={false} />
}
