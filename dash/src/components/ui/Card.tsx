"use client";

import { type ReactNode, type MouseEventHandler } from "react";
import { motion } from "motion/react";
import Image from "next/image";
import clsx from "clsx";

type CardVariant = "default" | "outlined" | "elevated";

type CardProps = {
	children: ReactNode;
	className?: string;
	variant?: CardVariant;
	onClick?: MouseEventHandler<HTMLDivElement>;
	interactive?: boolean;
};

type CardHeaderProps = {
	children: ReactNode;
	className?: string;
};

type CardBodyProps = {
	children: ReactNode;
	className?: string;
};

type CardFooterProps = {
	children: ReactNode;
	className?: string;
};

type CardMediaProps = {
	src: string;
	alt: string;
	className?: string;
	aspectRatio?: "video" | "square" | "wide";
};

const variantStyles: Record<CardVariant, string> = {
	default: "bg-zinc-800",
	outlined: "bg-transparent border border-zinc-700",
	elevated: "bg-zinc-800 shadow-lg shadow-black/20",
};

const aspectRatioStyles: Record<string, string> = {
	video: "aspect-video",
	square: "aspect-square",
	wide: "aspect-[21/9]",
};

export function Card({
	children,
	className,
	variant = "default",
	onClick,
	interactive = false,
}: CardProps) {
	const isClickable = onClick || interactive;

	return (
		<motion.div
			whileHover={isClickable ? { scale: 1.02 } : undefined}
			whileTap={isClickable ? { scale: 0.98 } : undefined}
			className={clsx(
				"rounded-xl text-white",
				variantStyles[variant],
				isClickable && "cursor-pointer transition-colors hover:bg-zinc-700",
				className,
			)}
			onClick={onClick}
			role={isClickable ? "button" : undefined}
			tabIndex={isClickable ? 0 : undefined}
			onKeyDown={
				isClickable
					? (e) => {
							if (e.key === "Enter" || e.key === " ") {
								e.preventDefault();
								onClick?.(e as unknown as React.MouseEvent<HTMLDivElement>);
							}
						}
					: undefined
			}
		>
			{children}
		</motion.div>
	);
}

export function CardHeader({ children, className }: CardHeaderProps) {
	return (
		<div
			className={clsx(
				"border-b border-zinc-700 px-4 py-3 sm:px-6 sm:py-4",
				className,
			)}
		>
			{children}
		</div>
	);
}

export function CardBody({ children, className }: CardBodyProps) {
	return (
		<div className={clsx("px-4 py-3 sm:px-6 sm:py-4", className)}>
			{children}
		</div>
	);
}

export function CardFooter({ children, className }: CardFooterProps) {
	return (
		<div
			className={clsx(
				"border-t border-zinc-700 px-4 py-3 sm:px-6 sm:py-4",
				className,
			)}
		>
			{children}
		</div>
	);
}

export function CardMedia({
	src,
	alt,
	className,
	aspectRatio = "video",
}: CardMediaProps) {
	return (
		<div
			className={clsx(
				"relative overflow-hidden rounded-t-xl",
				aspectRatioStyles[aspectRatio],
				className,
			)}
		>
			<Image
				src={src}
				alt={alt}
				fill
				className="object-cover"
				sizes="(max-width: 640px) 100vw, (max-width: 1024px) 50vw, 33vw"
			/>
		</div>
	);
}

export default Card;
