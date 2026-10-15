/**
 * MongoDB Models Index
 * 
 * Các models này tương ứng với collections trong MongoDB database
 * và mapping với UI types trong src/types/index.ts
 */

// Export all models
export * from './user.models';
export * from './wallet.models';
export * from './transaction.models';
export * from './merchant.models';
export * from './notification.models';

// --- TYPE HELPERS FOR MONGODB OPERATIONS ---

// Thêm _id vào type (khi đọc từ MongoDB)
export type WithId<T> = T & { _id: string };

// Loại bỏ _id khỏi type
export type WithoutId<T> = Omit<T, '_id'>;

// Type cho create operation (không cần _id)
export type CreateModel<T> = Omit<T, '_id'>;

// Type cho update operation (partial, không cần _id)
export type UpdateModel<T> = Partial<Omit<T, '_id'>>;

// --- UI TO MODEL MAPPING HELPERS ---

/**
 * Mapping helpers để convert giữa UI types (types/index.ts) 
 * và MongoDB models
 */

// Example: Convert UI User to UserModel
export interface UIToModelMapper {
  // Add mapping functions here when needed
}
