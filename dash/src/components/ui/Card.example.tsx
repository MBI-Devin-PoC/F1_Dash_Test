"use client";

import {
	Card,
	CardHeader,
	CardBody,
	CardFooter,
	CardMedia,
} from "./Card";
import Button from "./Button";

export default function CardExamples() {
	return (
		<div className="space-y-8 p-4 sm:p-6 lg:p-8">
			<h1 className="text-2xl font-bold text-white sm:text-3xl">
				Card Component Examples
			</h1>

			<section className="space-y-4">
				<h2 className="text-lg font-semibold text-zinc-300">Basic Card</h2>
				<Card className="max-w-sm">
					<CardBody>
						<p className="text-zinc-300">
							A simple card with just body content. Perfect for displaying basic
							information.
						</p>
					</CardBody>
				</Card>
			</section>

			<section className="space-y-4">
				<h2 className="text-lg font-semibold text-zinc-300">
					Card with Header and Footer
				</h2>
				<Card className="max-w-md">
					<CardHeader>
						<h3 className="text-lg font-semibold">Race Statistics</h3>
					</CardHeader>
					<CardBody>
						<div className="space-y-2">
							<p className="text-zinc-300">Lap Time: 1:23.456</p>
							<p className="text-zinc-300">Position: P1</p>
							<p className="text-zinc-300">Gap: +0.000s</p>
						</div>
					</CardBody>
					<CardFooter>
						<p className="text-sm text-zinc-400">Last updated: Just now</p>
					</CardFooter>
				</Card>
			</section>

			<section className="space-y-4">
				<h2 className="text-lg font-semibold text-zinc-300">Card Variants</h2>
				<div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
					<Card variant="default">
						<CardBody>
							<h3 className="mb-2 font-semibold">Default</h3>
							<p className="text-sm text-zinc-300">
								Standard card with solid background
							</p>
						</CardBody>
					</Card>

					<Card variant="outlined">
						<CardBody>
							<h3 className="mb-2 font-semibold">Outlined</h3>
							<p className="text-sm text-zinc-300">
								Transparent with border styling
							</p>
						</CardBody>
					</Card>

					<Card variant="elevated">
						<CardBody>
							<h3 className="mb-2 font-semibold">Elevated</h3>
							<p className="text-sm text-zinc-300">
								Raised appearance with shadow
							</p>
						</CardBody>
					</Card>
				</div>
			</section>

			<section className="space-y-4">
				<h2 className="text-lg font-semibold text-zinc-300">Interactive Card</h2>
				<Card
					className="max-w-sm"
					interactive
					onClick={() => alert("Card clicked!")}
				>
					<CardBody>
						<h3 className="mb-2 font-semibold">Click Me</h3>
						<p className="text-sm text-zinc-300">
							This card is interactive and responds to clicks and keyboard
							navigation.
						</p>
					</CardBody>
				</Card>
			</section>

			<section className="space-y-4">
				<h2 className="text-lg font-semibold text-zinc-300">Card with Media</h2>
				<Card className="max-w-sm">
					<CardMedia
						src="https://images.unsplash.com/photo-1504707748692-419802cf939d?w=800"
						alt="Formula 1 race car"
						aspectRatio="video"
					/>
					<CardBody>
						<h3 className="mb-2 font-semibold">Monaco Grand Prix</h3>
						<p className="text-sm text-zinc-300">
							Experience the thrill of the most prestigious race on the F1
							calendar.
						</p>
					</CardBody>
					<CardFooter className="flex justify-end">
						<Button onClick={() => alert("View details")}>View Details</Button>
					</CardFooter>
				</Card>
			</section>

			<section className="space-y-4">
				<h2 className="text-lg font-semibold text-zinc-300">
					Responsive Grid Layout
				</h2>
				<div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
					{[1, 2, 3, 4].map((i) => (
						<Card key={i} variant="elevated">
							<CardBody>
								<h3 className="mb-2 font-semibold">Driver {i}</h3>
								<p className="text-sm text-zinc-300">
									Card {i} in a responsive grid that adapts to screen size.
								</p>
							</CardBody>
						</Card>
					))}
				</div>
			</section>

			<section className="space-y-4">
				<h2 className="text-lg font-semibold text-zinc-300">
					Different Aspect Ratios
				</h2>
				<div className="grid gap-4 sm:grid-cols-3">
					<Card>
						<CardMedia
							src="https://images.unsplash.com/photo-1504707748692-419802cf939d?w=400"
							alt="Video aspect ratio"
							aspectRatio="video"
						/>
						<CardBody>
							<p className="text-sm text-zinc-300">Video (16:9)</p>
						</CardBody>
					</Card>

					<Card>
						<CardMedia
							src="https://images.unsplash.com/photo-1504707748692-419802cf939d?w=400"
							alt="Square aspect ratio"
							aspectRatio="square"
						/>
						<CardBody>
							<p className="text-sm text-zinc-300">Square (1:1)</p>
						</CardBody>
					</Card>

					<Card>
						<CardMedia
							src="https://images.unsplash.com/photo-1504707748692-419802cf939d?w=400"
							alt="Wide aspect ratio"
							aspectRatio="wide"
						/>
						<CardBody>
							<p className="text-sm text-zinc-300">Wide (21:9)</p>
						</CardBody>
					</Card>
				</div>
			</section>
		</div>
	);
}
